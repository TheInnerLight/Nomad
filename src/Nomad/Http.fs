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
    private 
    |Informational1xx of int
    |Success2xx of int
    |Redirection3xx of int
    |ClientError4xx of int
    |ServerError5xx of int

[<AutoOpen>]
module HttpResponseStatusPatterns =
    let (|Informational|Success|Redirection|ClientError|ServerError|) = function
        |Informational1xx i -> Informational (100 + i)
        |Success2xx i       -> Success       (200 + i)
        |Redirection3xx i   -> Redirection   (300 + i)
        |ClientError4xx i   -> ClientError   (400 + i)
        |ServerError5xx i   -> ServerError   (500 + i)

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

    let tryCreateStatusFromCode code =
        if code >= 100 && code < 200 then
            Some <| Informational1xx (code-100)
        else if code >= 200 && code < 300 then
            Some <| Success2xx (code-200)
        else if code >= 300 && code < 400 then
            Some <| Redirection3xx (code-300)
        else if code >= 400 && code < 500 then
            Some <| ClientError4xx (code-400)
        else if code >= 500 && code < 600 then
            Some <| ServerError5xx (code-500)
        else None

    let tryCreateRequestMethodFromString = function
        |"GET" -> Some Get
        |"POST" -> Some Post
        |"PUT" -> Some Put
        |"PATCH" -> Some Patch
        |"DELETE" -> Some Delete
        |_ -> None

    let Ok = Success2xx 00

    let BadRequest = ClientError4xx 00

    let Unauthorised = ClientError4xx 01

    let Forbidden = ClientError4xx 03

    let NotFound = ClientError4xx 04

    let UnprocessableEntity = ClientError4xx 22

    let RangeNotSatisfiable = ClientError4xx 16

    let MethodNotAllowed = ClientError4xx 05

