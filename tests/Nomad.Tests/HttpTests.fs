namespace Nomad.UnitTests

#nowarn "0025"

open System
open Xunit
open FsCheck
open Nomad
open Nomad.Routing
open FsCheck.Xunit
open Microsoft.AspNetCore.Http

module HttpGen =
    let httpCodeGen =
        Gen.choose (100, 599)
        |> Arb.fromGen

    let nonHttpCodeGen =
        Gen.choose (0, 1000)
        |> Arb.fromGen
        |> Arb.filter (fun c -> c < 100 || c >= 600)

type AllRange =
    static member Int32() =
        HttpGen.httpCodeGen

type OutsideRange =
    static member Int32() =
        HttpGen.nonHttpCodeGen

type InformationalRange =
    static member Int32() =
        HttpGen.httpCodeGen
        |> Arb.filter (fun c -> c < 200)

type SuccessRange =
    static member Int32() =
        HttpGen.httpCodeGen
        |> Arb.filter (fun c -> c >= 200 && c < 300)

type RedirectionRange =
    static member Int32() =
        HttpGen.httpCodeGen
        |> Arb.filter (fun c -> c >= 300 && c < 400)

type ClientErrorRange =
    static member Int32() =
        HttpGen.httpCodeGen
        |> Arb.filter (fun c -> c >= 400 && c < 500)

type ServerErrorRange =
    static member Int32() =
        HttpGen.httpCodeGen
        |> Arb.filter (fun c -> c >= 500 && c < 600)

type HttpTests() =
    [<Property( Arbitrary=[| typeof<AllRange> |] ) >]
    member this.``Given an Http Status Code in Range 100-599, tryCreateStatusFromCode always creates "Some"" status`` (code : int) =
        match Http.tryCreateStatusFromCode code with
        |Some _ -> true
        |None -> false

    [<Property( Arbitrary=[| typeof<OutsideRange> |] ) >]
    member this.``Given an Http Status Code outside Range 100-599, tryCreateStatusFromCode always returns None`` (code : int) =
        match Http.tryCreateStatusFromCode code with
        |Some _ -> false
        |None -> true

    [<Property( Arbitrary=[| typeof<AllRange> |] ) >]
    member this.``Given an Http Status Code in Range 100-599, running responseCode on value from tryCreateStatusFromCode will recover the original value `` (code : int) =
        match Http.tryCreateStatusFromCode code with
        |Some status -> Http.responseCode status = code
        |None -> false

    [<Property( Arbitrary=[| typeof<InformationalRange> |] ) >]
    member this.``Given an Http Status Code in Range 100-199, Information pattern always matches`` (code : int) =
        let (Some status) = Http.tryCreateStatusFromCode code
        match status with
        |Informational code' -> code = code'
        |_ -> false

    [<Property( Arbitrary=[| typeof<SuccessRange> |] ) >]
    member this.``Given an Http Status Code in Range 200-299, Success pattern always matches`` (code : int) =
        let (Some status) = Http.tryCreateStatusFromCode code
        match status with
        |Success code' -> code = code'
        |_ -> false

    [<Property( Arbitrary=[| typeof<RedirectionRange> |] ) >]
    member this.``Given an Http Status Code in Range 300-399, Redirection pattern always matches`` (code : int) =
        let (Some status) = Http.tryCreateStatusFromCode code
        match status with
        |Redirection code' -> code = code'
        |_ -> false

    [<Property( Arbitrary=[| typeof<ClientErrorRange> |] ) >]
    member this.``Given an Http Status Code in Range 400-499, ClientError pattern always matches`` (code : int) =
        let (Some status) = Http.tryCreateStatusFromCode code
        match status with
        |ClientError code' -> code = code'
        |_ -> false

    [<Property( Arbitrary=[| typeof<ServerErrorRange> |] ) >]
    member this.``Given an Http Status Code in Range 500-599, ServerError pattern always matches`` (code : int) =
        let (Some status) = Http.tryCreateStatusFromCode code
        match status with
        |ServerError code' -> code = code'
        |_ -> false

    [<Property(MaxTest = 1)>]
    member this.``Given Http.Ok, Success 200 pattern matches``() =
        match Http.Ok with
        |Success 200 -> true
        |_ -> false

    [<Property(MaxTest = 1)>]
    member this.``Given Http.BadRequest, ClientError 400 pattern matches``() =
        match Http.BadRequest with
        |ClientError 400 -> true
        |_ -> false

    [<Property(MaxTest = 1)>]
    member this.``Given Http.Unauthorised, ClientError 401 pattern matches``() =
        match Http.Unauthorised with
        |ClientError 401 -> true
        |_ -> false

    [<Property(MaxTest = 1)>]
    member this.``Given Http.Forbidden, ClientError 403 pattern matches``() =
        match Http.Forbidden with
        |ClientError 403 -> true
        |_ -> false

    [<Property(MaxTest = 1)>]
    member this.``Given Http.NotFound, ClientError 404 pattern matches``() =
        match Http.NotFound with
        |ClientError 404 -> true
        |_ -> false

    [<Property(MaxTest = 1)>]
    member this.``Given Http.MethodNotAllowed, ClientError 405 pattern matches``() =
        match Http.MethodNotAllowed with
        |ClientError 405 -> true
        |_ -> false

    
    [<Property(MaxTest = 1)>]
    member this.``Given Http.RangeNotSatisfiable, ClientError 416 pattern matches``() =
        match Http.RangeNotSatisfiable with
        |ClientError 416 -> true
        |_ -> false

    [<Property(MaxTest = 1)>]
    member this.``Given Http.UnprocessableEntity, ClientError 422 pattern matches``() =
        match Http.UnprocessableEntity with
        |ClientError 422 -> true
        |_ -> false