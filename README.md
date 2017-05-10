# Nomad
Nomad is an F# wrapper for ASP.NET Core designed to make backend development simple and hassle free.

## Getting Started

Let's start by showing a simple "Hello World!" application written using Nomad.

```fsharp
open Nomad
open HttpHandler

[<EntryPoint>]
let main argv = 
    let helloWorld = 
        setStatus Http.Ok 
        *> writeText "Hello World!"

    Nomad.run {Nomad.defaultConfig with RouteConfig = helloWorld}
```

This application responds to any incoming http request by setting a "200 OK" response and writing "Hello World" to the response body.

## Programming Model

The Nomad programming model is built on top of `HttpHandler`s, values of which are responsible for handling HTTP requests.

Many primitive HttpHandlers are provided which can perform atomic operations in the Http Context such as setting the response body, setting the response code, reading from the request body, etc.

Writing examples:

```fsharp
/// Create an http handler that sets the response content type to a supplied content type
setContentType : MimeType -> HttpHandler<unit>

/// Create an http handler that sets the response status to a supplied status
setStatus : HttpResponseStatus -> HttpHandler<unit>

/// Create an http handler that writes some supplied UTF-8 text to the response body
writeText : string -> HttpHandler<unit>
```

Reading examples:

```fsharp
/// Create an http handler that gets the request headers
getReqHeaders : HttpHandler<HttpHeaders>

/// Create an http handler that reads all of the bytes from the request body
readToEnd : HttpHandler<byte[]>
```

## Routing

Routing is also handled by another `HttpHandler` called `routeScan`.  This handler determines whether the HttpRequest matches the supplied route pattern.

```fsharp
let home() =
  setStatus Http.Ok 
  *> writeText "Welcome Home!"

let homeRoute = routeScan "/home" >>= home
```

We can also use F# print format syntax to create typesafe routing:

```fsharp
let greet name =
    setStatus Http.Ok 
    *> writeText (sprintf "Hello %s!" name)

let greetRoute = routeScan "/%s" >>= greet
```

Here the `%s` token tells the routeScan function to match any `string` and the `>>=` operator passes that `string` to the `greet` function.

### Multiple Routes

We can handle multiple routes using the choose combinator:

```fsharp
let testRoutes =
    choose [
        routeScan "/home" >>= home
        routeScan "/%s" >>= greet
    ]
```

The choose combinator has type `HttpHandler<'a> list -> HttpHandler<'a>`, it simply tries all of the `HttpHandler<'a>`s in the supplied list until it finds one of them that can handle the request.

Since we are just combining `HttpHandler`s, it's really easy to add a catch all operation for error handling.

```fsharp
let testRoutes =
    choose [
        routeScan "/home" >>= home
        routeScan "/%s" >>= greet
        Responses.``Not Found``
    ]
```

### Request Verbs

Handling different http request verbs (e.g. GET, POST, etc) can be done by using the `handleVerbs` function in conjunction with the `defaultVerbs` value.  `defaultVerbs` returns a record that contains handlers for each Http Verb, each of the verbs is initialised with a default handler that returns an Error 405 - Method Not Allowed response.

You can supply implementations for specific verbs using standard F# record `with` syntax.

```fsharp
    let getHandler() =
        handleVerbs {
            defaultVerbs with
                Get = 
                    setStatus Http.Ok
                    *> writeText "Hello Get!"
                Post = 
                    setStatus Http.Ok
                    *> writeText "Hello Post!"
        }
```
