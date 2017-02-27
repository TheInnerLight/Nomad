// Learn more about F# at http://fsharp.org
namespace Nomad

open HttpHandler

module TestServer =
    let testHandler1 x = handler {
        do! setStatus Http.Ok
        do! writeText <| sprintf "Hello World! %i" x
        }

    let testHandler2 (x, y) = handler {
        do! setStatus Http.Ok
        do! writeText <| sprintf "Hello Galaxy! %i %i" x y
        }

    let testHandler3 (x, y, z) = handler {
        do! setStatus Http.Ok
        do! setContentType ContentType.``text/html``
        do! writeText <| sprintf "<p>Hello Universe! %i %i %s</p>" x y z
        }

    let testRoutes =
        choose [
            routeScan "/%i" >>= testHandler1
            routeScan "/%i/%i" >>= testHandler2
            routeScan "/%i/%i/%s" >>= testHandler3
        ]

    [<EntryPoint>]
    let main argv = 
        Nomad.run {RouteConfig = testRoutes}
        0 // return an integer exit code
