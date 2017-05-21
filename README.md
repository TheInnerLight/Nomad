# Nomad
Nomad is an F# wrapper for ASP.NET Core designed to make backend development simple and hassle free.

## Project Status

Appveyor: ![Build Status](https://ci.appveyor.com/api/projects/status/github/TheInnerLight/Nomad?branch=master&svg=true)

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

Routing functionality is provided by the `Nomad.Routing` namespace.

The routing (`===>`) operator is used to indicate that requests satisfying the route criteria should be routed to a specific handler.

```fsharp
open Nomad
open Nomad.Routing
open HttpHandler

let home =
  setStatus Http.Ok 
  *> writeText "Welcome Home!"

let homeRoute = constant "home" ===> home
```

This routes traffic from `/home` to the `home` handler.

We can also create typesafe routing:

```fsharp
let greet name =
    setStatus Http.Ok 
    *> writeText (sprintf "Hello %s!" name)

let greetRoute = constant "greet" </> strR ===> home
```

This routes traffic from `/greet/[name]` to the `greet` handler.  The `name` argument accepted by the `greet` handler will be populated by the string from the route.

Supported typesafe routes are :

| route     | F# type       | .NET type 
|-----------|---------------|----------------
| `strR`    | `string`      | `System.String`
| `intR`    | `int`         | `System.Int32`
| `int64R`  | `int64`       | `System.Int64`
| `uintR`   | `uint`        | `System.UInt32`
| `uint64R` | `uint64`      | `System.UInt64`
| `floatR`  | `float`       | `System.Double`
| `guidR`   | N/A           | `System.Guid`


### Multiple Routes

We can handle multiple routes using the choose combinator:

```fsharp
let testRoutes =
    choose [
        constant "home"              ===> home
        constant "greet" </> strR    ===> home
    ]
```

The choose combinator has type `HttpHandler<'a> list -> HttpHandler<'a>`, it simply tries all of the `HttpHandler<'a>`s in the supplied list until it finds one of them that can handle the request.

Since we are just combining `HttpHandler`s, it's really easy to add a catch all operation for error handling.

```fsharp
let testRoutes =
    choose [
        constant "home"              ===> home
        constant "greet" </> strR    ===> home
        Responses.``Not Found``
    ]
```

## Request Verbs

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
