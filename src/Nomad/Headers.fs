namespace Nomad

open System.IO
open Microsoft.AspNetCore.Http
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

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HttpHeaders =
    open HeaderParsers.RangeParsers

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

    let tryGetAccept = function
        |HttpHeaders headers ->
            headers.Accept
            |> Option.ofObj
            |> Option.map (Seq.sortByDescending snd << Seq.map (fun x -> {TopLevel = TopLevelMime.fromString x.Type; SubType = x.SubType},Option.ofNullable x.Quality))

    let tryGetAcceptCharset = function
        |HttpHeaders headers ->
            headers.AcceptCharset
            |> Option.ofObj
            |> Option.map (Seq.sortByDescending snd << Seq.map (fun x -> x.Value, Option.ofNullable x.Quality))

                

