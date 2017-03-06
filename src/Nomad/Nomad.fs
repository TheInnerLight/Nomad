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
    let runContextWith handler (ctx : Microsoft.AspNetCore.Http.HttpContext) : System.Threading.Tasks.Task =
        HttpHandler.runHandler handler ctx
        |> Async.map (ignore)
        |> Async.startAsPlainTaskWithCancellation ctx.RequestAborted

    let run nc =
        WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(Directory.GetCurrentDirectory())
            .Configure(fun app -> 
                app
                    .UseDeveloperExceptionPage()
                    .Run (fun ctx -> runContextWith (nc.RouteConfig) ctx))
            .Build()
            .Run()