namespace Nomad

open System.IO
open Microsoft.AspNetCore.Http
open Microsoft.Net.Http.Headers
open FParsec

exception HeaderNotFoundException of string

module private HeaderParsers =
    let inline (!>>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)

    module RangeParsers =
        let pRangeUnit<'a> : Parser<_, 'a>  = manyCharsTill anyChar (pchar '=')

        let int64OrEof<'a> : Parser<_, 'a> = spaces >>. ((attempt pint64 |>> Some) <|> (eof |>> (fun _ -> None)))

        let firstRange<'a> : Parser<_, 'a>  = pint64 .>> pchar '-' .>>. int64OrEof

        let nextRanges<'a> : Parser<_, 'a> = many (pchar ',' >>. spaces >>. pint64 .>> pchar '-' .>>. pint64)

open HeaderParsers

type RangeHeader =
    |StartOnlyRange of string * int64
    |StartEndRanges of string * (int64*int64) list

type Cookie =
    {
        Name : string
        Value : string
    }

module private InlineHeaderParsers =
    let inline tryParseHeader< ^T when ^T : (static member TryParse : string * byref< ^ T > -> bool)> inputs =
        let res = ref Unchecked.defaultof<'T>
        if ((^T) : (static member TryParse : string * byref< ^ T > -> bool) (inputs, &res.contents)) then
            Result.Ok !res
        else 
            Result.Error <| ParseException "Failed to parse header"

    let inline tryParseHeaderList< ^T when ^T : (static member TryParseList : System.Collections.Generic.IList<string> * byref<System.Collections.Generic.IList< ^ T >> -> bool)> inputs =
        let res = ref Unchecked.defaultof<System.Collections.Generic.IList<'T>>
        if ((^T) : (static member TryParseList : System.Collections.Generic.IList<string> * byref<System.Collections.Generic.IList< ^ T >> -> bool) (inputs, &res.contents)) then
            Result.Ok !res
        else 
            Result.Error <| ParseException "Failed to parse header"

    let inline tryGetHeader< ^T when ^T : (static member TryParse : string * byref< ^ T > -> bool)> name = function
        |HttpHeaders headers ->
            match headers.Headers.TryGetValue(name) with
            |true, headerStrVals ->
                let headerStr : string = !>> headerStrVals
                tryParseHeader< ^T > headerStr
            |_ -> Result.Error << HeaderNotFoundException <| sprintf  "%s header was not found" name
                                                
    let inline tryGetHeaderList< ^T when ^T : (static member TryParseList : System.Collections.Generic.IList<string> * byref<System.Collections.Generic.IList< ^ T>> -> bool)> name = function
        |HttpHeaders headers ->
            match headers.Headers.TryGetValue(name) with
            |true, headerStrVals ->
                tryParseHeaderList< ^T > headerStrVals
            |_ -> Result.Error << HeaderNotFoundException <| sprintf  "%s header was not found" name

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

    let tryGetAccept headers = 
        tryGetHeaderList<MediaTypeHeaderValue> (HeaderNames.Accept) headers
        |> Result.map (fun mediaType ->
                mediaType
                |> Seq.map (fun x -> {TopLevel = TopLevelMime.fromString x.Type; SubType = x.SubType}, Option.ofNullable x.Quality)
                |> Seq.sortByDescending snd
                |> List.ofSeq)

    let tryGetStringWithQuality name headers =
        tryGetHeaderList<StringWithQualityHeaderValue> name headers
        |> Result.map (fun stringQals ->
            stringQals
            |> Seq.map (fun x -> x.Value, Option.ofNullable x.Quality)
            |> Seq.sortByDescending snd
            |> List.ofSeq)

    let tryGetAcceptCharset = tryGetStringWithQuality HeaderNames.AcceptCharset

    let tryGetAcceptEncoding = tryGetStringWithQuality HeaderNames.AcceptEncoding

    let tryGetAcceptLanguage = tryGetStringWithQuality HeaderNames.AcceptLanguage

    let tryGetContentRange header = 
        tryGetHeader<ContentRangeHeaderValue> HeaderNames.ContentRange header

    let tryGetCookie header =
        tryGetHeaderList<CookieHeaderValue> HeaderNames.Cookie header
        |> Result.map (List.ofSeq << Seq.map (fun cookie -> {Name = cookie.Name; Value = cookie.Value}))