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

    let Unauthorised = ClientError4xx 01

    let Forbidden = ClientError4xx 03

    let NotFound = ClientError4xx 04

    let UnprocessableEntity = ClientError4xx 22

    let RangeNotSatisfiable = ClientError4xx 16

    let MethodNotAllowed = ClientError4xx 05

[<Struct>]
type HandleState<'T> =
    |Continue of 'T
    |Unhandled

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

    /// Uses a supplied http handler only if the request verb matches the supplied verb
    let inline filterVerb verb route = 
        HttpHandler (fun ctx -> 
            if Http.requestMethod ctx.Request.Method = verb then
                runHandler route ctx
            else
                Async.return' Unhandled)

module HttpHandler =

    /// Create an http handler that gets the request headers
    let getReqHeaders = InternalHandlers.withContext (fun ctx -> HttpHeaders <| ctx.Request.GetTypedHeaders())

    /// Create an http handler that sets the response content type to a supplied content type
    let setContentType contentType = InternalHandlers.withContext (fun ctx -> ctx.Response.ContentType <- ContentType.asString contentType)

    /// Create an http handler that sets the response status to a supplied status
    let setStatus status = InternalHandlers.withContext (fun ctx -> ctx.Response.StatusCode <- Http.responseCode status)

    /// Create an http handler that reads all of the bytes from the request body
    let readToEnd = InternalHandlers.withContextAsync (fun ctx -> 
        async {
            match Option.ofNullable ctx.Request.ContentLength with
            |Some length ->
                return! ctx.Request.Body.AsyncRead (int length)
            |None ->
                use stream = new MemoryStream()
                do! Async.awaitPlainTask <| ctx.Request.Body.CopyToAsync(stream)
                return stream.ToArray()
        })

    /// Create an http handler that writes some supplied bytes to the response body
    let writeBytes bytes = InternalHandlers.withContextAsync (fun ctx -> ctx.Response.Body.AsyncWrite bytes)

    /// Create an http handler that writes some supplied UTF-8 text to the response body
    let writeText (text : string) = InternalHandlers.withContextAsync (fun ctx -> ctx.Response.Body.AsyncWrite <| System.Text.Encoding.UTF8.GetBytes(text))

    /// Create an http handler that does not handle the request
    let unhandled = HttpHandler (fun _ -> Async.return' Unhandled)

    /// Create an http handler  that returns the supplied result
    let return' x =  HttpHandler (fun _  -> Async.return' <| Continue x)

    /// Lift an asychronous operation as an http handler 
    let liftAsync x = HttpHandler (fun _ -> Async.map Continue x)

    /// Monadic bind for http handlers
    let bind f x = HttpHandler (fun ctx ->
        InternalHandlers.runHandler x ctx
        |> Async.bind (fun x' ->
            match x' with
            |Continue(value) -> InternalHandlers.runHandler (f value) ctx
            |Unhandled -> Async.return' Unhandled))

    /// Functor map for http handlers
    let map f x = HttpHandler (fun ctx ->
        InternalHandlers.runHandler x ctx
        |> Async.bind (fun x' ->
            match x' with
            |Continue(value) -> Async.return' <| Continue (f value) 
            |Unhandled -> Async.return' Unhandled))

    /// Sequential application of http handlers
    let apply f x = bind (fun fe -> map fe x) f

    /// Uses a supplied http handler only if the request is a GET request
    let get route = InternalHandlers.filterVerb Get route

   /// Uses a supplied http handler only if the request is a POST request
    let post route = InternalHandlers.filterVerb Post route

    /// An http request handler that tries each of the supplied list of handlers in turn until one of them succeeds
    let choose routes =
        let rec firstM routes ctx =
            async{
                match routes with
                |[] -> return Unhandled
                |route::routes' ->
                    let! route = InternalHandlers.runHandler route ctx
                    match route with
                    |Continue value -> return Continue(value)
                    |Unhandled -> return! firstM routes' ctx
            }
        HttpHandler (fun ctx -> firstM routes ctx)

    /// Runs a list of Http Handlers in sequence aggregating all of the results that succeed into a list
    let sequence routes =
        let rec seqRec routes running ctx =
            async{
                match routes with
                |[] -> return Continue (running)
                |route::routes' ->
                    let! route = InternalHandlers.runHandler route ctx
                    match route with
                    |Continue value -> return! seqRec routes' (value :: running) ctx
                    |Unhandled -> return! seqRec routes' (running) ctx
            }
        HttpHandler (fun ctx -> seqRec routes [] ctx)

    let sequenceIgnore routes = map (ignore) (sequence routes)

    /// An http request handler that handles routes of the supplied PrintFormat pattern
    let routeScan pattern =
        let binder (ctx : HttpContext) =
            match Sscanf.sscanf pattern (ctx.Request.Path.Value) with
            |Ok result -> return' <| result
            |Error _ -> unhandled
        bind binder InternalHandlers.askContext 

    /// A handler that caches the values from the supplied handler in order to derive the content length of the response
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
        |> Async.startAsPlainTaskWithCancellation ctx.RequestAborted

/// Computation builder for HttpHandlers
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
    /// Map operator for Http Handlers
    let inline (<!>) f x = HttpHandler.map f x
    /// Apply operator for Http Handlers
    let inline (<*>) f x = HttpHandler.apply f x
    /// Bind operator for Http Handlers
    let inline (>>=) x f = HttpHandler.bind f x
    /// Kleisli composition (composition of binding functions) operator for Http Handlers
    let inline (>=>) f g x = f x >>= g
    /// Sequence actions, discarding the value of the first argument.
    let inline ( *> ) u v = (fun _ x -> x) <!> u <*> v
    /// Sequence actions, discarding the value of the second argument.
    let inline ( <* ) u v = (fun x _ -> x) <!> u <*> v