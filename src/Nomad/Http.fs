namespace Nomad

open System.IO

type HttpVerb =
    |Get
    |Post
    |Put
    |Patch
    |Delete

type TopLevelMime =
    |Application
    |Audio
    |Example
    |Font
    |Image
    |Message
    |Model
    |Multipart
    |Text
    |Video

type MimeType = {TopLevel : TopLevelMime; SubType : string}

module ContentType =
    let asString mimeType =
        let {TopLevel = topLevel; SubType = subType} = mimeType
        match topLevel with
            |Application    -> sprintf "application/%s" subType
            |Audio          -> sprintf "audio/%s" subType
            |Example        -> sprintf "example/%s" subType
            |Font           -> sprintf "font/%s" subType
            |Image          -> sprintf "image/%s" subType
            |Message        -> sprintf "message/%s" subType
            |Model          -> sprintf "model/%s" subType
            |Multipart      -> sprintf "multipart/%s" subType
            |Text           -> sprintf "text/%s" subType
            |Video          -> sprintf "video/%s" subType

    let ``application/json`` = {TopLevel = Application; SubType = "json"}
    let ``application/xml`` = {TopLevel = Application; SubType = "xml"}
    let ``text/css`` = {TopLevel = Text; SubType = "css"}
    let ``text/html`` = {TopLevel = Text; SubType = "html"}
    let ``text/plain`` = {TopLevel = Text; SubType = "plain"}

type HttpResponseStatus =
    |Informational1xx of int
    |Success2xx of int
    |Redirection3xx of int
    |ClientError4xx of int
    |ServerError5xx of int

type HttpRequest = {
    Method : HttpVerb
    PathString : string
    QueryString : string
    }

type HttpResponse = {
    Status : HttpResponseStatus
    ContentType : MimeType
    Body : System.IO.Stream -> Async<unit>
    }

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

type HttpHandler<'U> = HttpHandler of (HttpRequest -> HttpResponse -> ('U * HttpResponse) option)

module HttpHandler =
    let runHandler = function
        |HttpHandler g -> g

    let getResponse = HttpHandler (fun _ resp -> Some(resp,resp))
    let putResponse x = HttpHandler (fun _ resp -> Some((),x))
    let modifyResponse f = HttpHandler (fun _ resp -> Some((),f resp))
    let askRequest =  HttpHandler (fun req resp -> Some(req,resp))

    let setStatus status = modifyResponse (fun resp -> {resp with Status = status})

    let setContentType contentType = modifyResponse (fun resp -> {resp with ContentType = contentType})

    let writeToBody f = modifyResponse (fun resp -> {resp with Body = fun s -> async.Bind(resp.Body s, fun _ -> f s)})

    let writeBytes b = writeToBody (fun s -> s.AsyncWrite b)

    let writeText (t : string) = writeToBody (fun s -> s.AsyncWrite <| System.Text.Encoding.UTF8.GetBytes(t))

    let return' x =  HttpHandler (fun _  resp -> Some(x, resp))

    let zero =  HttpHandler (fun _  resp -> Some((), resp))

    let unhandled = HttpHandler (fun _  _ -> None)

    let bind x f = 
        HttpHandler (fun req resp ->
            match runHandler x req resp with
            |Some (a, resp') -> runHandler (f a) req resp'
            |None -> None)

    let map f x = 
        HttpHandler (fun req resp ->
            match runHandler x req resp with
            |Some (a, resp') -> Some (f a, resp')
            |None -> None)

    let apply f  x = bind f (fun fe -> map fe x)

    let choose routes =
        HttpHandler (fun req resp ->
            routes
            |> Seq.map (fun h -> runHandler h req resp)
            |> Seq.find (Option.isSome))

    let routeScan pattern =
        let binder req =
            match Sscanf.sscanf pattern req.PathString with
            |Ok result -> return' result
            |Error _ -> unhandled
        bind askRequest binder

type HttpHandlerBuilder() =

    member this.Return x = HttpHandler.return' x
    member this.ReturnFrom x : HttpHandler<'U> = x
    member this.Bind (x, f) = HttpHandler.bind x f
    member this.Zero() = HttpHandler.zero

[<AutoOpen>]
module Prelude =
    let handler = HttpHandlerBuilder()
    let inline (<!>) f x = HttpHandler.map f x
    let inline (<*>) f x = HttpHandler.apply f x
    let inline (>>=) x f = HttpHandler.bind x f
    let inline (>=>) f g x = f x >>= g

    let inline ( *> ) u v = (fun _ x -> x) <!> u <*> v
    let inline ( <* ) u v = (fun x _ -> x) <!> u <*> v