// Learn more about F# at http://fsharp.org
namespace Nomad

open System
open System.IO
open System.Text
open System.Text.RegularExpressions
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.Configuration
open Microsoft.FSharp.Reflection

module TestServer =
    let testHandler1 x = handler {
        do! HttpHandler.setStatus Http.Ok
        do! HttpHandler.writeText <| sprintf "Hello World! %i" x
        }

    let testHandler2 (x, y) = handler {
        do! HttpHandler.setStatus Http.Ok
        do! HttpHandler.writeText <| sprintf "Hello Galaxy! %i %i" x y
        }

    let testHandler3 (x, y, z) = handler {
        do! HttpHandler.setStatus Http.Ok
        do! HttpHandler.writeText <| sprintf "Hello Universe! %i %i %s" x y z
        }

    let testRoutes =
        HttpHandler.choose [
            HttpHandler.routeScan "/%i" >>= testHandler1
            HttpHandler.routeScan "/%i/%i" >>= testHandler2
            HttpHandler.routeScan "/%i/%i/%s" >>= testHandler3
        ]

    [<EntryPoint>]
    let main argv = 
        Nomad.run {RouteConfig = testRoutes}
        0 // return an integer exit code
