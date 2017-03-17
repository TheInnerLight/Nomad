namespace Nomad

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
    |Other of string

type MimeType = {TopLevel : TopLevelMime; SubType : string}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module TopLevelMime =
    let fromString = function
        |"application"  -> Application
        |"audio"        -> Audio
        |"example"      -> Example
        |"font"         -> Font
        |"image"        -> Image
        |"message"      -> Message
        |"model"        -> Model
        |"multipart"    -> Multipart
        |"text"         -> Text
        |"video"        -> Video
        |other          -> Other other

    let toString = function
        |Application    -> "application" 
        |Audio          -> "audio" 
        |Example        -> "example" 
        |Font           -> "font" 
        |Image          -> "image" 
        |Message        -> "message" 
        |Model          -> "model"
        |Multipart      -> "multipart"
        |Text           -> "text"
        |Video          -> "video"
        |Other(topType) -> topType

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ContentType =
    let asString mimeType =
        let {TopLevel = topLevel; SubType = subType} = mimeType
        sprintf "%s/%s" (TopLevelMime.toString topLevel) subType

    let ``application/json`` = {TopLevel = Application; SubType = "json"}
    let ``application/xml`` = {TopLevel = Application; SubType = "xml"}
    let ``image/png`` = {TopLevel = Image; SubType = "png"}
    let ``text/css`` = {TopLevel = Text; SubType = "css"}
    let ``text/html`` = {TopLevel = Text; SubType = "html"}
    let ``text/plain`` = {TopLevel = Text; SubType = "plain"}
    let ``video/mp4`` = {TopLevel = Video; SubType = "mp4"}