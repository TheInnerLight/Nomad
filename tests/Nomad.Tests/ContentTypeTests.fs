namespace Nomad.UnitTests

#nowarn "0025"

open System
open FsCheck
open Nomad
open FsCheck.Xunit
open Microsoft.AspNetCore.Http

type ContentTypeTests() =
    [<Property(MaxTest = 1)>]
    member this.``TopLevelMime toString for Application type returns "application" `` () =
        TopLevelMime.toString Application = "application"

    [<Property(MaxTest = 1)>]
    member this.``TopLevelMime toString for Audio type returns "audio" `` () =
        TopLevelMime.toString Audio = "audio"

    [<Property(MaxTest = 1)>]
    member this.``TopLevelMime toString for Example type returns "example" `` () =
        TopLevelMime.toString Example = "example"

    [<Property(MaxTest = 1)>]
    member this.``TopLevelMime toString for Font type returns "font" `` () =
        TopLevelMime.toString Font = "font"

    [<Property(MaxTest = 1)>]
    member this.``TopLevelMime toString for Image type returns "image" `` () =
        TopLevelMime.toString Image = "image"

    [<Property(MaxTest = 1)>]
    member this.``TopLevelMime toString for Message type returns "message" `` () =
        TopLevelMime.toString Message = "message"

    [<Property(MaxTest = 1)>]
    member this.``TopLevelMime toString for Model type returns "model" `` () =
        TopLevelMime.toString Model = "model"

    [<Property(MaxTest = 1)>]
    member this.``TopLevelMime toString for Multipart type returns "multipart" `` () =
        TopLevelMime.toString Multipart = "multipart"

    [<Property(MaxTest = 1)>]
    member this.``TopLevelMime toString for Text type returns "text" `` () =
        TopLevelMime.toString Text = "text"

    [<Property(MaxTest = 1)>]
    member this.``TopLevelMime toString for Video type returns "video" `` () =
        TopLevelMime.toString Video = "video"

    [<Property>]
    member this.``TopLevelMime toString for Other type returns supplied string `` (other : NonEmptyString) =
        TopLevelMime.toString (Other other.Get) = other.Get