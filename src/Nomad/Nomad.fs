namespace Nomad

open System.IO
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Google
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Routing
open Microsoft.AspNetCore.ResponseCompression
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Primitives
open Microsoft.FSharp.Reflection
open Nomad.Files
open HttpHandler

type AuthenticationType =
    |CookieAuth of CookieAuthenticationOptions
    |JwtBearerAuth of JwtBearerOptions

type NomadConfig = {RouteConfig : HttpHandler<unit>; AuthTypes : AuthenticationType list; ResponseCompression : bool}

module Nomad =

    let defaultConfig = {RouteConfig = return' (); AuthTypes = []; ResponseCompression = false}

    let runRoutes rc (app : IApplicationBuilder) =
        app.Run(fun ctx -> 
            ctx.Response.Headers.["Server"] <- (StringValues "Nomad")
            runContextWith rc ctx)

    let configureAuthList auths builder =
        let folder (acc : IApplicationBuilder) authOpt =
            match authOpt with
            |CookieAuth cookieAuthOpts -> acc.UseCookieAuthentication(cookieAuthOpts)
            |JwtBearerAuth jwtBearerAuthOpts -> acc.UseJwtBearerAuthentication(jwtBearerAuthOpts)
        List.fold folder builder auths

    let useDeveloperExceptionPage (app : IApplicationBuilder) =
        app.UseDeveloperExceptionPage()

    let useResponseCompression rc (app : IApplicationBuilder) =
        if rc then app.UseResponseCompression() else app

    let run nc =
        WebHostBuilder()
            .UseKestrel(fun opts -> opts.ThreadCount <- 12)
            .UseContentRoot(Directory.GetCurrentDirectory())
            .ConfigureServices(fun serv -> 
                ignore <| serv.AddAuthentication()
                ignore <| serv.AddResponseCompression()
                )
            .Configure(fun app -> 
                app
                |> configureAuthList nc.AuthTypes
                |> useResponseCompression nc.ResponseCompression
                |> useDeveloperExceptionPage
                |> runRoutes nc.RouteConfig)        
            .Build()
            .Run()