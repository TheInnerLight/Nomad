namespace Nomad

open System.IO
open Microsoft.AspNetCore.Http

type HttpVerb =
    |Get
    |Post
    |Put
    |Patch
    |Delete

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

    let NotFound = ClientError4xx 04

    let UnprocessableEntity = ClientError4xx 22

[<Struct>]
type HandleState<'T> =
    |Continue of 'T
    |ShortCircuit

[<Struct>]
type HttpHandler<'U> = internal HttpHandler of (HttpContext -> Async<HandleState<'U>>)

[<Struct>]
type HttpHeaders = internal HttpHeaders of Headers.RequestHeaders

module internal InternalHandlers =
    let askContext = HttpHandler (async.Return << Continue)

    let inline withContext f = HttpHandler (async.Return << Continue << f)

    let inline withContextAsync f = HttpHandler (Async.map (Continue) << f)

    let inline runHandler hand = 
        match hand with
        |HttpHandler g -> g

module HttpHandler =

    let getReqHeaders = InternalHandlers.withContext (fun ctx -> HttpHeaders <| ctx.Request.GetTypedHeaders())

    let setContentType contentType = InternalHandlers.withContext (fun ctx -> ctx.Response.ContentType <- ContentType.asString contentType)

    let setStatus status = InternalHandlers.withContext (fun ctx -> ctx.Response.StatusCode <- Http.responseCode status)

    let writeBytes bytes = InternalHandlers.withContextAsync (fun ctx -> ctx.Response.Body.AsyncWrite bytes)

    let writeText (text : string) = InternalHandlers.withContextAsync (fun ctx -> ctx.Response.Body.AsyncWrite <| System.Text.Encoding.UTF8.GetBytes(text))

    let unhandled = HttpHandler (fun _ -> Async.return' ShortCircuit)

    let return' x =  HttpHandler (fun _  -> Async.return' <| Continue x)

    let liftAsync x = HttpHandler (fun _ -> Async.map Continue x)

    let bind f x = HttpHandler (fun ctx ->
        InternalHandlers.runHandler x ctx
        |> Async.bind (fun x' ->
            match x' with
            |Continue(value) -> InternalHandlers.runHandler (f value) ctx
            |ShortCircuit -> Async.return' ShortCircuit))

    let map f x = HttpHandler (fun ctx ->
        InternalHandlers.runHandler x ctx
        |> Async.bind (fun x' ->
            match x' with
            |Continue(value) -> Async.return' <| Continue (f value) 
            |ShortCircuit -> Async.return' ShortCircuit))

    let apply f x = bind (fun fe -> map fe x) f

    let choose routes =
        let rec firstM routes ctx =
            async{
                match routes with
                |[] -> return ShortCircuit
                |route::routes' ->
                    let! route = InternalHandlers.runHandler route ctx
                    match route with
                    |Continue value -> return Continue(value)
                    |ShortCircuit -> return! firstM routes' ctx
            }
        HttpHandler (fun ctx -> firstM routes ctx)

    let routeScan pattern =
        let binder (ctx : HttpContext) =
            match Sscanf.sscanf pattern (ctx.Request.Path.Value) with
            |Ok result -> return' <| result
            |Error _ -> unhandled
        bind binder InternalHandlers.askContext 

    let deriveContentLength handler = HttpHandler (fun ctx ->
        let oldBody = ctx.Response.Body
        let newBody = new System.IO.MemoryStream()
        ctx.Response.Body <- newBody
        InternalHandlers.runHandler handler ctx
        |> Async.bind  (fun res ->
            ctx.Response.ContentLength <- System.Nullable(newBody.Length)
            ctx.Response.Body <- oldBody
            newBody.Position <- 0L
            newBody.CopyToAsync oldBody
            |> Async.AwaitTask
            |> Async.bind (fun _ -> Async.return' res)))
           
    let internal runContextWith handler (ctx : HttpContext) : System.Threading.Tasks.Task =
        InternalHandlers.runHandler handler ctx
        |> Async.map (ignore)
        |> Async.bind (fun _ -> Async.AwaitTask (ctx.Response.Body.FlushAsync()))
        |> Async.startAsPlainTaskWithCancellation ctx.RequestAborted

type HttpHandlerBuilder() =
    member this.Return x = HttpHandler.return' x
    member this.ReturnFrom x : HttpHandler<'U> = x
    member this.Bind (x, f) = HttpHandler.bind f x
    member this.TryFinally(body, compensation) =
        HttpHandler (fun ctx ->
            async.TryFinally(InternalHandlers.runHandler body ctx, compensation))
    member this.Using(disposable:#System.IDisposable, body) =
        this.TryFinally(body disposable, fun () -> if isNull disposable then () else disposable.Dispose())


[<AutoOpen>]
module Prelude =
    let handler = HttpHandlerBuilder()
    let inline (<!>) f x = HttpHandler.map f x
    let inline (<*>) f x = HttpHandler.apply f x
    let inline (>>=) x f = HttpHandler.bind f x
    let inline (>=>) f g x = f x >>= g
    let inline ( *> ) u v = (fun _ x -> x) <!> u <*> v
    let inline ( <* ) u v = (fun x _ -> x) <!> u <*> v