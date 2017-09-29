// Learn more about F# at http://fsharp.org
namespace Nomad.TestApp

open Nomad.Authentication
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System.Security.Claims
open Nomad
open Nomad.Routing
open Nomad.QueryParams
open Nomad.Authentication
open Nomad.Files
open Nomad.Errors
open Nomad.Verbs
open HttpHandler

module Controllers =
    let loginController =
        handleVerbs {
            defaultVerbs with
                Get = 
                    writeText "Please log in"
                Post = 
                    handler {
                        let! result = readToEnd
                        return! signIn "MyCookieMiddlewareInstance" (fun _ -> Result.Ok <| ClaimsPrincipal()) result
                    }
        }

module TestServer =
    let testHandler1 = 
        queryParams (stringParam "name" <&> intParam "reg") (fun name shipReg ->
            setStatus Http.Ok 
            *> setContentType ContentType.``text/plain``
            *> writeText (sprintf "Hello World! %s %i" name shipReg))

    let testHandler2 x y =
        setStatus Http.Ok 
        *> writeText (sprintf "Hello Galaxy! %i %i" x y)

    let testHandler3 x y z = 
        deriveContentLength <|
            setStatus Http.Ok 
            *> setContentType ContentType.``text/html`` 
            *> writeText  (sprintf "<p>Hello Universe! %i %i %s</p>" x y z)

    let testHandler4 =
        setStatus Http.Ok
        *> setContentType ContentType.``video/mp4``
        *> getReqHeaders
        *> writeFileRespectingRangeHeaders """movie.mp4"""

    let testRoutes =
        choose [
            constant "kirk"                                 ===> testHandler1
            intR </> intR                                   ===> testHandler2
            constant "test" </> intR </> intR </> strR      ===> testHandler3
            constant "video.mp4"                            ===> testHandler4
            constant "login"                                ===> Controllers.loginController
            notFound
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

        Nomad.run {RouteConfig = testRoutes; AuthTypes = [CookieAuth cookieAuthOpts]; ResponseCompression = true}

        0 // return an integer exit code
