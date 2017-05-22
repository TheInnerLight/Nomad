namespace Nomad

open System.IO
open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers
open FParsec

exception HeaderNotFoundException of string
exception ParseException of string

module private HeaderParsers =
    let inline (!>>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)

    module RangeParsers =
        let pRangeUnit<'a> : Parser<_, 'a>  = manyCharsTill anyChar (pchar '=')

        let int64OrEof<'a> : Parser<_, 'a> = spaces >>. ((attempt pint64 |>> Some) <|> (eof |>> (fun _ -> None)))

        let firstRange<'a> : Parser<_, 'a>  = pint64 .>> pchar '-' .>>. int64OrEof

        let nextRanges<'a> : Parser<_, 'a> = many (pchar ',' >>. spaces >>. pint64 .>> pchar '-' .>>. pint64)

open HeaderParsers

/// A range header
type RangeHeader =
    |StartOnlyRange of string * int64
    |StartEndRanges of string * (int64*int64) list

/// A content range header
type ContentRangeHeader = 
    |UnitRangeAndSize of unit : string * startRange : int64 * endRange : int64 * size : int64
    |UnitAndRange of unit : string * startRange : int64 * endRange : int64
    |UnitAndSize of unit : string * size : int64

/// A cookie header
type Cookie = {
    /// The name of the cookie
    Name : string
    /// The cookie value
    Value : string
    }

/// An entity tag header
type ETag =
    /// An entity tag with weak validation
    |Weak of string
    /// An entity tag with strong validation
    |Strong of string

module private InlineHeaderParsers =
    /// Try to parse a string as some type ^T that exposes a TryParse static method
    let inline tryParseHeader< ^T when ^T : (static member TryParse : string * byref< ^ T > -> bool)> inputs =
        let res = ref Unchecked.defaultof<'T>
        if ((^T) : (static member TryParse : string * byref< ^ T > -> bool) (inputs, &res.contents)) then
            Result.Ok !res
        else 
            Result.Error <| ParseException "Failed to parse header"

    /// Try to parse a string as a list of some type ^T that exposes a TryParseList static method
    let inline tryParseHeaderList< ^T when ^T : (static member TryParseList : System.Collections.Generic.IList<string> * byref<System.Collections.Generic.IList< ^ T >> -> bool)> inputs =
        let res = ref Unchecked.defaultof<System.Collections.Generic.IList<'T>>
        if ((^T) : (static member TryParseList : System.Collections.Generic.IList<string> * byref<System.Collections.Generic.IList< ^ T >> -> bool) (inputs, &res.contents)) then
            Result.Ok !res
        else 
            Result.Error <| ParseException "Failed to parse header"
    
    /// Try to parse an HTTP Header with the supplied name as some type ^T that exposes a TryParse static method
    let inline tryGetHeader< ^T when ^T : (static member TryParse : string * byref< ^ T > -> bool)> name = function
        |HttpHeaders headers ->
            match headers.Headers.TryGetValue(name) with
            |true, headerStrVals ->
                let headerStr : string = !>> headerStrVals
                tryParseHeader< ^T > headerStr
            |_ -> Result.Error << HeaderNotFoundException <| sprintf  "%s header was not found" name
         
    /// Try to parse an HTTP Header with the supplied name as a list of some type ^T that exposes a TryParseList static method
    let inline tryGetHeaderList< ^T when ^T : (static member TryParseList : System.Collections.Generic.IList<string> * byref<System.Collections.Generic.IList< ^ T>> -> bool)> name = function
        |HttpHeaders headers ->
            match headers.Headers.TryGetValue(name) with
            |true, headerStrVals ->
                tryParseHeaderList< ^T > headerStrVals
            |_ -> Result.Error << HeaderNotFoundException <| sprintf  "%s header was not found" name

    /// Try to get an ETag header list for the supplied header name
    let tryGetETagHeaderList headerName headers = 
        tryGetHeaderList<EntityTagHeaderValue> headerName headers
        |> Result.map (List.ofSeq << Seq.map (fun tag ->
            if tag.IsWeak then
                Weak tag.Tag
            else
                Strong tag.Tag))

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HttpHeaders =
    open HeaderParsers.RangeParsers
    open InlineHeaderParsers

    let tryParseRangeHeader = function
        |HttpHeaders headers ->
            match headers.Headers.TryGetValue("Range") with
            |true, headerStrVals ->
                let headerStr : string = !>> headerStrVals
                match run (tuple3 pRangeUnit firstRange nextRanges) (headerStr) with
                // matches patterns such as Range: bytes=0-
                |Success ((units, (start', None), []), _, _)             -> Result.Ok <| StartOnlyRange (units, start')
                // matches patterns such as Range: bytes=0-1023
                |Success ((units, (start', Some end'), []), _, _)        -> Result.Ok <| StartEndRanges (units, [start', end'])
                // matches patterns such as Range: bytes=0-1023,4096-65535,..
                |Success ((units, (start', Some end'), startEnds), _, _) -> Result.Ok <| StartEndRanges (units, (start', end') :: startEnds)
                // anything else is a parse failure
                |_                                                       -> Result.Error <| ParseException "Failed to parse range header"
            |false, _ -> Result.Error <| HeaderNotFoundException "Range header was not found"


    let tryGetStringWithQuality name headers =
        tryGetHeaderList<StringWithQualityHeaderValue> name headers
        |> Result.map (fun stringQals ->
            stringQals
            |> Seq.map (fun x -> x.Value, Option.ofNullable x.Quality)
            |> Seq.sortByDescending snd
            |> List.ofSeq)

    /// Try to get the 'Accept' header value - the content types acceptable for the response
    let tryGetAccept headers = 
        tryGetHeaderList<MediaTypeHeaderValue> (HeaderNames.Accept) headers
        |> Result.map (fun mediaType ->
                mediaType
                |> Seq.map (fun x -> {TopLevel = TopLevelMime.fromString x.Type; SubType = x.SubType}, Option.ofNullable x.Quality)
                |> Seq.sortByDescending snd
                |> List.ofSeq)

    /// Try to get the 'Accept-Charset' header value: the character sets that are acceptable for the response
    let tryGetAcceptCharset = tryGetStringWithQuality HeaderNames.AcceptCharset

    /// Try to get the 'Accept-Encoding' header value: the encodings that are acceptable for the response
    let tryGetAcceptEncoding = tryGetStringWithQuality HeaderNames.AcceptEncoding

    /// Try to get the 'Accept-Language' header value: the languages that are acceptable for the response
    let tryGetAcceptLanguage = tryGetStringWithQuality HeaderNames.AcceptLanguage

    /// Try to get the content length header
    let tryGetContentLength = tryGetHeader<int64> HeaderNames.ContentLength

    /// Try to get the content range header values
    let tryGetContentRange header = 
        tryGetHeader<ContentRangeHeaderValue> HeaderNames.ContentRange header
        |> Result.bind (fun crhv ->
            match (crhv.Unit, Option.ofNullable crhv.From, Option.ofNullable crhv.To, Option.ofNullable crhv.Length) with
            |(u, Some(from), Some(to'), Some(length))  -> Result.Ok <| UnitRangeAndSize (u, from, to', length)
            |(u, Some(from), Some(to'), None)          -> Result.Ok <| UnitAndRange (u, from, to')
            |(u, None, None, Some(length))             -> Result.Ok <| UnitAndSize (u, length)
            |_                                         -> Result.Error <| ParseException "Failed to parse Content Range header")

    /// Try to get the cookie header values
    let tryGetCookies header =
        tryGetHeaderList<CookieHeaderValue> HeaderNames.Cookie header
        |> Result.map (List.ofSeq << Seq.map (fun cookie -> {Name = cookie.Name; Value = cookie.Value}))

    /// Try to get the date header values
    let tryGetDate = tryGetHeader<System.DateTime> HeaderNames.Date

    /// Try to get the If-Match header values
    let tryGetIfMatch headers = tryGetETagHeaderList HeaderNames.IfMatch headers

    /// Try to get the 'If-Modified-Since' header value
    let tryGetIfModifiedSince = tryGetHeader<System.DateTime> HeaderNames.IfModifiedSince

    /// Try to get the If-None-Match header values
    let tryGetIfNoneMatch headers = tryGetETagHeaderList HeaderNames.IfNoneMatch headers

    /// Try to get the 'If-Unmodified-Since' header value
    let tryGetIfUnmodifiedSince = tryGetHeader<System.DateTime> HeaderNames.IfUnmodifiedSince


        
    