namespace Nomad

open System.IO
open Microsoft.AspNetCore.Http
open FParsec

exception HeaderNotFoundException of string

module HeaderParsers =
    let inline (!>>) (x:^a) : ^b = ((^a or ^b) : (static member op_Implicit : ^a -> ^b) x)

    module RangeParsers =
        let pRangeUnit<'a> : Parser<_, 'a>  = pstring "Range:" >>. manyCharsTill anyChar (pchar '=')

        let int64OrEof<'a> : Parser<_, 'a> = spaces >>. ((attempt pint64 |>> Some) <|> (eof |>> (fun _ -> None)))

        let firstRange<'a> : Parser<_, 'a>  = pint64 .>> pchar '-' .>>. int64OrEof

        let nextRanges<'a> : Parser<_, 'a> = many (pchar ',' >>. spaces >>. pint64 .>> pchar '-' .>>. pint64)

open HeaderParsers

type RangeHeader =
    |StartOnlyRange of string * int64
    |StartEndRanges of string * (int64*int64) list

module HttpHeaders =
    open HeaderParsers.RangeParsers

    let tryParseRangeHeader (headers : IHeaderDictionary) =
        match headers.TryGetValue("Range") with
        |true, headerStrVals ->
            let headerStr : string = !>> headerStrVals
            match run (tuple3 pRangeUnit firstRange nextRanges) (headerStr) with
            |Success ((units, (start', None), []), _, _)             -> Result.Ok <| StartOnlyRange (units, start')
            |Success ((units, (start', Some end'), []), _, _)        -> Result.Ok <| StartEndRanges (units, [start', end'])
            |Success ((units, (start', Some end'), startEnds), _, _) -> Result.Ok <| StartEndRanges (units, (start', end') :: startEnds)
            |_ -> Result.Error <| ParseException "Failed to parse range header"
        |false, _ -> Result.Error <| HeaderNotFoundException "Range header was not found"
                

