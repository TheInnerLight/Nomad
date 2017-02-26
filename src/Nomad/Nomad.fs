namespace Nomad

open System.IO
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Routing
open Microsoft.Extensions.Configuration
open Microsoft.FSharp.Reflection

module Async =
    let inline startAsPlainTask (work : Async<unit>) : System.Threading.Tasks.Task = System.Threading.Tasks.Task.Factory.StartNew(fun () -> work |> Async.RunSynchronously)

type Startup() = 
    let runContextWith handler (ctx : HttpContext) : System.Threading.Tasks.Task =
        let reqType = Http.requestMethod <| ctx.Request.Method
        let req' = {Method = reqType; PathString = ctx.Request.Path.Value; QueryString = ctx.Request.QueryString.Value}
        match HttpHandler.runHandler handler req' {Status = ClientError4xx(04); Body = fun _ -> async.Return() } with
        |Some (_,resp) ->
            ctx.Response.StatusCode <- Http.responseCode resp.Status
            ctx.Response.Body 
            |> resp.Body
            |> Async.startAsPlainTask
        |None -> async.Return () |> Async.startAsPlainTask

    static member val RouteHandler = Unchecked.defaultof<HttpHandler<unit>> with get, set

    member this.Configure (app : IApplicationBuilder) = 
        app.Run (fun ctx -> runContextWith (Startup.RouteHandler) ctx)

type NomadConfig = {RouteConfig : HttpHandler<unit>}

module Nomad =
    let run nc =
        Startup.RouteHandler <- nc.RouteConfig
        WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(Directory.GetCurrentDirectory())
            .UseConfiguration(ConfigurationBuilder()
                                .Build())
            .UseStartup<Startup>()
            .Build()
            .Run()