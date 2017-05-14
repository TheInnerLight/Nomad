namespace Nomad.Routing

open Nomad
open FParsec

type private RParser<'a> = Parser<'a, unit>

module private SharedParsers =
    let endCond : RParser<unit> = skipChar '/'

type private IRoute<'a, 'b> = 
    abstract member Run : 'b -> string -> ('a * string) option

type private ConstantRoute<'a>(constRoute : string) =
    interface IRoute<'a, 'a> with
        member this.Run a str =
            match run (skipString constRoute) str with
            |Success(res, _, pos) -> Some (a, (str.Substring(int <| pos.Index)))
            |_ -> None

type private ParseRoute<'a,'b>(parser : RParser<'b>) =
    interface IRoute<'a, 'b -> 'a> with
        member this.Run (f : 'b -> 'a) str =
            match run parser str with
            |Success(res, _, pos) -> Some (f res, (str.Substring(int <| pos.Index)))
            |_ -> None

type private PairRoute<'a,'b,'c>(one : IRoute<'b,'c>, two : IRoute<'a, 'b>) = 
    interface IRoute<'a, 'c> with
        member this.Run a str =
            one.Run a str
            |> Option.bind (fun (b, str') -> two.Run b str')

type Route<'a,'b> = private Route of IRoute<'a,'b>

module Routing = 

    let constant path = Route(ConstantRoute(path))

    let (</>) ((Route one) : Route<'b,'c>) ((Route two) : Route<'a, 'b>) : Route<'a, 'c> = 
        let (Route slash) = constant "/"
        let one' = PairRoute(one, slash)
        Route(PairRoute(one', two))

    let strR<'a> : Route<'a, _> = Route(ParseRoute(manyCharsTill anyChar SharedParsers.endCond))

    let intR<'a> : Route<'a, _>  = Route(ParseRoute(pint32))

    let test : Route<unit,_> = constant "starship" </> intR </> constant "captain" </> strR </> constant "cheese"

    let run ((Route route) : Route<'a,'b>) f str =
        route.Run f str

module HttpHandler =
    let route ((Route route) : Route<HttpHandler<'a>,'b>) (handlerFunc : 'b) : HttpHandler<'a> =
        let binder (ctx : Microsoft.AspNetCore.Http.HttpContext) =
            let path = ctx.Request.Path.Value
            match route.Run handlerFunc path with
            |Some (v, _) -> v
            |None -> HttpHandler.unhandled
        InternalHandlers.askContext
        |> HttpHandler.bind binder
        



    //ignore <| test.Run (fun i str -> printfn "%i %s" i str) "starship/5/captain/dhfsudhfdshf"