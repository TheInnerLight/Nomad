namespace Nomad

open System.IO
open Microsoft.AspNetCore.Http

type HttpVerb =
    |Get
    |Post
    |Put
    |Patch
    |Delete

module Async =
    let inline return' x = async.Return x

    let inline bind x f = async.Bind(x, f)

    let map f x = async.Bind(x, async.Return << f)

    let inline startAsPlainTask (work : Async<unit>) : System.Threading.Tasks.Task = 
        System.Threading.Tasks.Task.Factory.StartNew(fun () -> work |> Async.RunSynchronously)

type HttpResponseStatus =
    |Informational1xx of int
    |Success2xx of int
    |Redirection3xx of int
    |ClientError4xx of int
    |ServerError5xx of int

module Http =
    let responseString = function
        |Informational1xx i -> sprintf "1%02i" i
        |Success2xx i       -> sprintf "2%02i" i
        |Redirection3xx i   -> sprintf "3%02i" i
        |ClientError4xx i   -> sprintf "4%02i" i
        |ServerError5xx i   -> sprintf "5%02i" i

    let responseCode = function
        |Informational1xx i -> 100+i
        |Success2xx i       -> 200+i
        |Redirection3xx i   -> 300+i
        |ClientError4xx i   -> 400+i
        |ServerError5xx i   -> 500+i

    let requestMethod = function
        |"GET" -> Get
        |"POST" -> Post
        |"PUT" -> Put
        |"PATCH" -> Patch
        |"DELETE" -> Delete

    let Ok = Success2xx 00

    let BadRequest = ClientError4xx 00

    let Forbidden = ClientError4xx 03

    let UnprocessableEntity = ClientError4xx 22

[<Struct>]
type HttpHandler<'U> = internal HttpHandler of (Microsoft.AspNetCore.Http.HttpContext -> Async<'U option>)

[<Struct>]
type HttpHeaders = internal HttpHeaders of Microsoft.AspNetCore.Http.Headers.RequestHeaders

module HttpHandler =
    let runHandler = function
    |HttpHandler g -> g

    let askContext = HttpHandler (async.Return << Some)

    let withContext f = HttpHandler (async.Return << Some << f)

    let withContextAsync f = HttpHandler (Async.map (Some) << f)

    let getReqHeaders = withContext (fun ctx -> HttpHeaders <| ctx.Request.GetTypedHeaders())

    let setContentType contentType = withContext (fun ctx -> ctx.Response.ContentType <- ContentType.asString contentType)

    let setStatus status = withContext (fun ctx -> ctx.Response.StatusCode <- Http.responseCode status)

    let writeBytes bytes = withContextAsync (fun ctx -> ctx.Response.Body.AsyncWrite bytes)

    let writeText (text : string) = withContextAsync (fun ctx -> ctx.Response.Body.AsyncWrite <| System.Text.Encoding.UTF8.GetBytes(text))

    let unhandled = HttpHandler (fun _ -> Async.return' None)

    let return' x =  HttpHandler (fun _  -> Async.return' <| Some x)

    let liftAsync x = HttpHandler (fun _ -> Async.map Some x)

    let bind x f = HttpHandler (fun ctx ->
        Async.bind (runHandler x ctx) (fun x' ->
            match x' with
            |Some(value) -> runHandler (f value) ctx
            |None -> Async.return' None))

    let map f x = HttpHandler (fun ctx ->
        Async.bind (runHandler x ctx) (fun x' ->
            match x' with
            |Some(value) -> Async.return' <| Some (f value) 
            |None -> Async.return' None))

    let apply f x = bind f (fun fe -> map fe x)

    let choose routes =
        let rec firstM routes ctx =
            async{
                match routes with
                |[] -> return None
                |route::routes' ->
                    let! route = runHandler route ctx
                    match route with
                    |Some value -> return Some(value)
                    |None -> return! firstM routes' ctx
            }
        HttpHandler (fun ctx -> firstM routes ctx)

    let routeScan pattern =
        let binder (ctx : Microsoft.AspNetCore.Http.HttpContext) =
            match Sscanf.sscanf pattern (ctx.Request.Path.Value) with
            |Ok result -> return' <| result
            |Error _ -> unhandled
        bind askContext binder

type HttpHandlerBuilder() =
    member this.Return x = HttpHandler.return' x
    member this.ReturnFrom x : HttpHandler<'U> = x
    member this.Bind (x, f) = HttpHandler.bind x f
    member this.TryFinally(body, compensation) =
        HttpHandler (fun ctx ->
            async.TryFinally(HttpHandler.runHandler body ctx, compensation))
    member this.Using(disposable:#System.IDisposable, body) =
        this.TryFinally(body disposable, fun () -> if isNull disposable then () else disposable.Dispose())


    //member this.Zero() = HttpHandler.zero

[<AutoOpen>]
module Prelude =
    let handler = HttpHandlerBuilder()
    let inline (<!>) f x = HttpHandler.map f x
    let inline (<*>) f x = HttpHandler.apply f x
    let inline (>>=) x f = HttpHandler.bind x f
    let inline (>=>) f g x = f x >>= g
    let inline ( *> ) u v = (fun _ x -> x) <!> u <*> v
    let inline ( <* ) u v = (fun x _ -> x) <!> u <*> v