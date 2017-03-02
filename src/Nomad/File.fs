namespace Nomad.Files

open Nomad
open System.IO

type FilePart =
    |Complete
    |Part of int64 * int64

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module FilePart =
    let getLengthOrDefault defaultLength filePart =
        match filePart with
        |Complete -> defaultLength
        |Part (_, end') -> min defaultLength end'

    let getStartOrDefault maxStart filePart =
        match filePart with
        |Complete -> 0L
        |Part (start', _) -> min maxStart start'

[<AutoOpen>]
module HttpHandler =
    let private writePartialFile filePart file = 
        handler {
            use fs = new System.IO.FileStream (file, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read)
            let end' = FilePart.getLengthOrDefault fs.Length filePart
            let rec writeFileRec pos = handler {
                let dataLength = int <| min 262144L (end'-pos)
                match dataLength with
                |x when x <= 0 -> return ()
                |_ ->
                    let! data  = HttpHandler.liftAsync (fs.AsyncRead dataLength)
                    do! HttpHandler.writeBytes data
                    return! writeFileRec (pos+262144L)
                }
            return! writeFileRec (FilePart.getStartOrDefault fs.Length filePart)
        }

    let writeFile file = writePartialFile Complete file

    let writeFileRange start' end' file = writePartialFile (Part (start', end')) file