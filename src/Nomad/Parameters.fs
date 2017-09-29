namespace Nomad.QueryParams

open Nomad
open FParsec
open System.Collections.Generic
open Microsoft.Extensions.Primitives
open Microsoft.AspNetCore.Http

type private RParser<'a> = Parser<'a, unit>

type private IParam<'a, 'b> = 
    abstract member Run : 'b -> Dictionary<string, StringValues> -> 'a option

type private ParseParam<'a,'b>(key : string, parser : RParser<'b>) =
    interface IParam<'a, 'b -> 'a> with
        member this.Run (f : 'b -> 'a) qps = 
            match run parser (string (qps.[key])) with
            |Success(res, _, pos) -> Some (f res)
            |_ -> None

type private PairParam<'a,'b,'c>(one : IParam<'b,'c>, two : IParam<'a, 'b>) = 
    interface IParam<'a, 'c> with
        member this.Run a qps = 
            one.Run a qps
            |> Option.bind (fun b -> two.Run b qps)

type QueryParams<'a,'b> = private QueryParams of IParam<'a,'b>

[<AutoOpen>]
module QueryParams = 
    /// Match a string parameter with the supplied key
    let stringParam key = QueryParams(ParseParam(key, many anyChar |>> (fun x -> System.String.Join("",x))))

    /// Match an int parameter with the supplied key
    let intParam key = QueryParams(ParseParam(key, pint32))

    /// Match an int64 parameter with the supplied key
    let int64Param key = QueryParams(ParseParam(key, pint64))

    /// Match a uint parameter with the supplied key
    let uintParam key = QueryParams(ParseParam(key, puint32))

    /// Match a uint64 parameter with the supplied key
    let uint64Param key = QueryParams(ParseParam(key, puint64))

    /// Match a float parameter with the supplied key
    let floatParam key = QueryParams(ParseParam(key, pfloat))

    /// Match a guid parameter with the supplied key
    let guidParam key = QueryParams(ParseParam(key, many1Chars (hex <|> pchar '-') |>> System.Guid.Parse))

    let (<&>) ((QueryParams one) : QueryParams<'b,'c>) ((QueryParams two) : QueryParams<'a, 'b>) : QueryParams<'a, 'c> = 
        QueryParams(PairParam(one, two))

    let internal parameterise ((QueryParams route) : QueryParams<HttpHandler<'a>,'b>) (handlerFunc : 'b) : HttpHandler<'a> =
        let binder (ctx : HttpContext) =
            let path = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(ctx.Request.QueryString.Value)
            match route.Run handlerFunc path with
            |Some (v) -> v
            |None -> HttpHandler.unhandled
        InternalHandlers.askContext
        |> HttpHandler.bind binder


    let queryParams (rt : QueryParams<HttpHandler<'a>,'b>) (handlerFunc : 'b) : HttpHandler<'a> =
        parameterise rt handlerFunc
        