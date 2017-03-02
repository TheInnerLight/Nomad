// Learn more about F# at http://fsharp.org
namespace Nomad

open HttpHandler
open Nomad.Files

module TestServer =
    let testHandler1 x = 
        setStatus Http.Ok 
        *> writeText (sprintf "Hello World! %i" x)
        

    let testHandler2 (x, y) =
        setStatus Http.Ok 
        *> writeText (sprintf "Hello Galaxy! %i %i" x y)

    let testHandler3 (x, y, z) = 
        setStatus Http.Ok 
        *> setContentType ContentType.``text/html`` 
        *> writeText  (sprintf "<p>Hello Universe! %i %i %s</p>" x y z)

    let testHandler4() =
        setStatus Http.Ok
        *> setContentType ContentType.``video/mp4``
        *> writeFile """test.mp4"""


    let testRoutes =
        choose [
            routeScan "/%i" >>= testHandler1
            routeScan "/%i/%i" >>= testHandler2
            routeScan "/test/%i/%i/%s" >>= testHandler3
            routeScan "/video.mp4" >>= testHandler4
        ]

    [<EntryPoint>]
    let main argv = 
        Nomad.run {RouteConfig = testRoutes}
        0 // return an integer exit code
