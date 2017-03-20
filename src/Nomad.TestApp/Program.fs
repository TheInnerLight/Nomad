// Learn more about F# at http://fsharp.org
namespace Nomad

open Nomad.Files
open Nomad.Authentication
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System.Security.Claims
open HttpHandler

module Controllers =
    let loginController() =
        choose [
            get  <| writeText "Please log in"
            post <| 
                handler {
                    let! result = readToEnd()
                    return! signIn "MyCookieMiddlewareInstance" (fun _ -> Result.Ok <| ClaimsPrincipal()) result
                }
        ]

module TestServer =
    let testHandler1 x = 
        challenge "MyCookieMiddlewareInstance" <|
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
        >>= (fun x -> 
            match HttpHeaders.tryParseRangeHeader x with
            |Ok range -> writeFile """movie.mp4"""
            |Error err -> return' ())

    let testRoutes =
        choose [
            routeScan "/%i" >>= testHandler1
            routeScan "/%i/%i" >>= testHandler2
            routeScan "/test/%i/%i/%s" >>= testHandler3
            routeScan "/video.mp4" >>= testHandler4
            routeScan "/login" >>= Controllers.loginController
            Responses.``Not Found``
        ]

    [<EntryPoint>]
    let main argv = 

        let cookieAuthOpts = CookieAuthenticationOptions()
        cookieAuthOpts.AuthenticationScheme <- "MyCookieMiddlewareInstance"
        cookieAuthOpts.LoginPath <- PathString("/login")
        cookieAuthOpts.AutomaticAuthenticate <- true
        cookieAuthOpts.AutomaticChallenge <- true

        Nomad.run {RouteConfig = testRoutes; AuthTypes = [CookieAuth cookieAuthOpts]}
        0 // return an integer exit code
