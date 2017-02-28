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
    let runContextWith handler (ctx : HttpContext) : System.Threading.Tasks.Task =
        let reqType = Http.requestMethod <| ctx.Request.Method
        let req' = {Method = reqType; PathString = ctx.Request.Path.Value; QueryString = ctx.Request.QueryString.Value}
        match HttpHandler.runHandler handler req' {Status = ClientError4xx(04); ContentType = ContentType.``text/plain``; Body = fun _ _ -> async.Return() } with
        |Some (_,resp) ->
            ctx.Response.StatusCode <- Http.responseCode resp.Status
            ctx.Response.Headers.Add("Content-Type", StringValues(ContentType.asString resp.ContentType))
            resp.Body ctx.Request.Body ctx.Response.Body
            |> Async.startAsPlainTask
        |None -> async.Return () |> Async.startAsPlainTask

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