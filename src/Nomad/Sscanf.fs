namespace Nomad

open System
open System.Text
open System.Text.RegularExpressions
open Microsoft.FSharp.Reflection

exception ParseException of string

module Result =

    let lift2 f x y =
        x |> Result.map (fun x' -> y |> Result.map (fun y' -> f x' y')) |> Result.bind id

    let traverse mFunc lst =
        let consF x ys = lift2 (fun x y -> x :: y) (mFunc x) ys
        List.foldBack (consF) lst (Ok [])

    let sequence seq = traverse id seq

module Parsers = 
    let check f x = 
        if f x then Ok x
        else Error << ParseException <| sprintf "Format failure %s" x 

    let tryParseDecimal x = 
        match Decimal.TryParse x with
        |true, value -> Ok value
        |false, _ -> Error <| ParseException "Failed to parse decimal"

    let tryParseBool x =
        match Boolean.TryParse x with
        |true, value -> Ok value
        |false, _ -> Error <| ParseException "Failed to parse boolean"

    let tryParseInt x =
        match Int32.TryParse x with
        |true, value -> Ok value
        |false, _ -> Error <| ParseException "Failed to parse int"

    let tryParseUInt x =
        match UInt32.TryParse x with
        |true, value -> Ok value
        |false, _ -> Error <| ParseException "Failed to parse uint"

    let tryParseFloat x =
        match Double.TryParse x with
        |true, value -> Ok value
        |false, _ -> Error <| ParseException "Failed to parse float"

module Sscanf =
    open Parsers
    let parsers = 
        dict [
            'b', Result.map (box) << tryParseBool
            'd', Result.map (box) << tryParseInt
            'i', Result.map (box) << tryParseInt
            's', Result.map (box) << Ok
            'u', Result.map (box) << tryParseUInt
            'x', Result.map (box) << Result.map (tryParseInt) << Result.map ((+) "0x") << check (String.forall Char.IsLower) 
            'X', Result.map (box) << Result.map (tryParseInt) << Result.map ((+) "0x") << check (String.forall Char.IsUpper) 
            'o', Result.map (box) << tryParseInt << ((+) "0o")
            'e', Result.map (box) << tryParseFloat
            'E', Result.map (box) << tryParseFloat
            'f', Result.map (box) << tryParseFloat
            'F', Result.map (box) << tryParseFloat
            'g', Result.map (box) << tryParseFloat
            'G', Result.map (box) << tryParseFloat
            'M', Result.map (box) << tryParseDecimal
            'c', Result.map (box) << Ok << char
        ]

    // array of all possible formatters, i.e. [|"%b"; "%d"; ...|]
    let separators =
       parsers.Keys
       |> Seq.map (fun c -> "%" + sprintf "%c" c) 
       |> Seq.toArray

    // Creates a list of formatter characters from a format string,
    // for example "(%s,%d)" -> ['s', 'd']
    let rec getFormatters xs =
       match xs with
       |'%'::'%'::xr -> getFormatters xr
       |'%'::x::xr -> 
           if parsers.ContainsKey x then x::getFormatters xr
           else failwithf "Unknown formatter %%%c" x
       |x::xr -> getFormatters xr
       |[] -> []

    let formattersFor (pf:PrintfFormat<_,_,_,_,'t>) =
        pf.Value.ToCharArray() // need original string here (possibly with "%%"s)
        |> Array.toList 
        |> getFormatters 
    let sscanf (pf:PrintfFormat<_,_,_,_,'t>) s : Result<'t,_> =
        let formatStr = pf.Value.Replace("%%", "%")
        let constants = formatStr.Split(separators, StringSplitOptions.None)
        let regex = Regex("^" + String.Join("(.*?)", constants |> Array.map Regex.Escape) + "$")
        let formatters = formattersFor pf

        let groups = 
            regex.Match(s).Groups 
            |> Seq.cast<Group> 
            |> Seq.skip 1

        let matches =
            (groups, formatters)
            ||> Seq.map2 (fun g f -> g.Value |> parsers.[f])
            |> Seq.toList

        matches
        |> Result.sequence
        |> Result.map (fun matches' ->
            match matches' with
            |[m] -> m :?> 't
            |_ -> FSharpValue.MakeTuple(Array.ofList matches', typeof<'t>) :?> 't)