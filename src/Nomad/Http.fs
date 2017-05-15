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