// Learn more about F# at http://fsharp.org
namespace Nomad.TestApp

open Nomad.Authentication
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System.Security.Claims
open Nomad
open Nomad.Authentication
open Nomad.Files
open Nomad.Verbs
open HttpHandler

module Controllers =
    let loginController() =
        choose [
            get  <| writeText "Please log in"
            post <| 
                handler {
                    let! result = readToEnd
                    return! signIn "MyCookieMiddlewareInstance" (fun _ -> Result.Ok <| ClaimsPrincipal()) result
                }
        ]

module TestServer =
    let testHandler1 x = 
        requireAuth "MyCookieMiddlewareInstance" <|
            setStatus Http.Ok 
            *> writeText (sprintf "Hello World! %i" x)

    let testHandler2 (x, y) =
        setStatus Http.Ok 
        *> writeText (sprintf "Hello Galaxy! %i %i" x y)

    let testHandler3 (x, y, z) = 
        deriveContentLength <|
            setStatus Http.Ok 
            *> setContentType ContentType.``text/html`` 
            *> writeText  (sprintf "<p>Hello Universe! %i %i %s</p>" x y z)

    let testHandler4() =
        setStatus Http.Ok
        *> setContentType ContentType.``video/mp4``
        *> getReqHeaders
        *> writeFileRespectingRangeHeaders """movie.mp4"""

    let testHandler5() =
        handleVerbs {
            defaultVerbs with
                Get = 
                    setStatus Http.Ok
                    *> writeText "Hello Get!"
                Post = 
                    setStatus Http.Ok
                    *> writeText "Hello Post!"
        }

    let testRoutes =
        choose [
            routeScan "/%i" >>= testHandler1
            routeScan "/%i/%i" >>= testHandler2
            CaseInsensitive.routeScan "/test/%i/%i/%s" >>= testHandler3
            routeScan "/video.mp4" >>= testHandler4
            routeScan "/login" >>= Controllers.loginController
            Responses.``Not Found``
        ]

    [<EntryPoint>]
    let main argv = 

        let cookieAuthOpts = 
            CookieAuthenticationOptions (
                AuthenticationScheme = "MyCookieMiddlewareInstance",
                LoginPath = PathString "/login",
                AutomaticAuthenticate = true,
                AutomaticChallenge = true
                )

        Nomad.run {RouteConfig = testRoutes; AuthTypes = [CookieAuth cookieAuthOpts]}
        0 // return an integer exit code
