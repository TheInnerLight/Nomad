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

module Async =
    let inline startAsPlainTask (work : Async<unit>) : System.Threading.Tasks.Task = System.Threading.Tasks.Task.Factory.StartNew(fun () -> work |> Async.RunSynchronously)

type NomadConfig = {RouteConfig : HttpHandler<unit>}

module Nomad =
    let runContextWith handler (ctx : HttpContext) : System.Threading.Tasks.Task =
        let reqType = Http.requestMethod <| ctx.Request.Method
        let req' = {Method = reqType; PathString = ctx.Request.Path.Value; QueryString = ctx.Request.QueryString.Value}
        match HttpHandler.runHandler handler req' {Status = ClientError4xx(04); ContentType = ContentType.``text/plain``; Body = fun _ -> async.Return() } with
        |Some (_,resp) ->
            ctx.Response.StatusCode <- Http.responseCode resp.Status
            ctx.Response.Headers.Add("Content-Type", StringValues(ContentType.asString resp.ContentType))
            ctx.Response.Body 
            |> resp.Body
            |> Async.startAsPlainTask
        |None -> async.Return () |> Async.startAsPlainTask

    let run nc =
        WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(Directory.GetCurrentDirectory())
            .Configure(fun app -> 
                app.Run (fun ctx -> runContextWith (nc.RouteConfig) ctx))
            .Build()
            .Run()