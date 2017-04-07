namespace Nomad

open System.IO
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Google
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Primitives
open Microsoft.FSharp.Reflection
open Nomad.Files
open HttpHandler


type AuthenticationType =
    |CookieAuth of CookieAuthenticationOptions

type NomadConfig = {RouteConfig : HttpHandler<unit>; AuthTypes : AuthenticationType list}

module Nomad =

    let runRoutes rc (app : IApplicationBuilder) =
        app.Run(fun ctx -> 
            ctx.Response.Headers.["Server"] <- (StringValues "Nomad")
            runContextWith rc ctx)

    let configureAuthList auths builder =
        let folder (acc : IApplicationBuilder) authOpt =
            match authOpt with
            |CookieAuth cookieAuthOpts -> acc.UseCookieAuthentication(cookieAuthOpts)
        List.fold folder builder auths

    let useDeveloperExceptionPage (app : IApplicationBuilder) =
        app.UseDeveloperExceptionPage()

    let run nc =
        WebHostBuilder()
            .UseKestrel(fun opts -> opts.ThreadCount <- 12)
            .UseContentRoot(Directory.GetCurrentDirectory())
            .ConfigureServices(fun serv -> ignore <| serv.AddAuthentication())
            .Configure(fun app -> 
                app
                |> configureAuthList nc.AuthTypes
                |> useDeveloperExceptionPage
                |> runRoutes nc.RouteConfig)        
            .Build()
            .Run()