namespace Nomad

open System.IO
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Google
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Primitives
open Microsoft.FSharp.Reflection

type NomadConfig = {RouteConfig : HttpHandler<unit>}

module Nomad =

    let runInt nc ctx =
        HttpHandler.runContextWith (nc.RouteConfig) ctx

    let run nc =
        WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(Directory.GetCurrentDirectory())
            .Configure(fun app -> 
                app
                    .UseDeveloperExceptionPage()
                    .Run (fun ctx -> runInt nc ctx))
                        
            .Build()
            .Run()