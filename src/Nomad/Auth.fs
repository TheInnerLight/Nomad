namespace Nomad.Authentication

open Nomad
open HttpHandler

module HttpHandler =
    let authenticated handler =
        InternalHandlers.askContext
        |> bind (fun ctx -> 
            if not <| isNull ctx.User && ctx.User.Identity.IsAuthenticated then
                handler
            else
                Responses.Forbidden)