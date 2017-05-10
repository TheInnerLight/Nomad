namespace Nomad.UnitTests

open System
open FsCheck
open Nomad
open FsCheck.Xunit
open Microsoft.AspNetCore.Http

module RouteScanTests =
    [<Property>]
    let ``Given a routeScan using a constant route name, http request route must match`` () =
        let context = DefaultHttpContext()
        context.Request.Path <- PathString("/MyResource")
        let result = 
            HttpHandler.routeScan "/MyResource"
            |> HttpHandler.Unsafe.runHandler context
        result = Continue ()

    [<Property>]
    let ``Given a routeScan using a constant route name, http request route must NOT match if the casing is different`` () =
        let context = DefaultHttpContext()
        context.Request.Path <- PathString("/myresource")
        let result = 
            HttpHandler.routeScan "/MyResource"
            |> HttpHandler.Unsafe.runHandler context
        result <> Continue ()

    [<Property>]
    let ``Given a routeScan using an integer route format, http request route must match for any int`` (i : int) =
        let context = DefaultHttpContext()
        context.Request.Path <- PathString(sprintf "/MyResource/%d" i)
        let result = 
            HttpHandler.routeScan "/MyResource/%d"
            |> HttpHandler.Unsafe.runHandler context
        result = Continue (i)

    [<Property>]
    let ``Given a routeScan using double integer route format, http request route must match for any int`` (i : int, i2 : int) =
        let context = DefaultHttpContext()
        context.Request.Path <- PathString(sprintf "/MyResource/%d/Splitter/%d" i i2)
        let result = 
            HttpHandler.routeScan "/MyResource/%d/Splitter/%d"
            |> HttpHandler.Unsafe.runHandler context
        result = Continue (i, i2)

    [<Property>]
    let ``Given a routeScan using a float route format, http request route must match for any float`` (flt : NormalFloat) =
        let context = DefaultHttpContext()
        context.Request.Path <- PathString(sprintf "/MyResource/%f" flt.Get)
        let result = 
            HttpHandler.routeScan "/MyResource/%f"
            |> HttpHandler.Unsafe.runHandler context
        let (Continue fltResult) = result
        result = Continue (float <| sprintf "%f" (flt.Get))

    [<Property>]
    let ``Given a routeScan using an unsigned integer route format, http request route must match for any uint`` (u : uint32) =
        let context = DefaultHttpContext()
        context.Request.Path <- PathString(sprintf "/MyResource/%u" u)
        let result = 
            HttpHandler.routeScan "/MyResource/%u"
            |> HttpHandler.Unsafe.runHandler context
        result = Continue (u)

    [<Property>]
    let ``Given a routeScan using a cgar route format, http request route must match for any char`` (c : char) =
        let context = DefaultHttpContext()
        context.Request.Path <- PathString(sprintf "/MyResource/%c" c)
        let result = 
            HttpHandler.routeScan "/MyResource/%c"
            |> HttpHandler.Unsafe.runHandler context
        result = Continue (c)