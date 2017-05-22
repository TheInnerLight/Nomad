namespace Nomad.Errors

open Nomad
open HttpHandler

/// A set of generic template responses
module HttpHandler =
    let private errorPage code title descr = 
        sprintf """
<html>
    <head>
        <title>Error %d - %s</title>
        <style>
        body {
          position: absolute;
          top: 50%%;
          left: 50%%;
          transform: translate(-50%%, -50%%);
          text-align: center;
        }
        </style>
    </head>
    <body>
        <p><h1>Error %d - %s</h1></p>
        <p>%s</p>
    </body>
</html>
        """ code title code title descr

    let private showErrorPage status title desc =
        setStatus status
        *> setContentType ContentType.``text/html``
        *> writeText (errorPage (Http.responseCode status) title desc)

    let notFound = showErrorPage Http.NotFound "Not Found" "The resource that you requested was not found."

    let forbidden = showErrorPage Http.Forbidden "Forbidden" "You do not have permission to access this resource."

    let unauthorised = showErrorPage Http.Unauthorised "Unauthorised" "Authentication is required to access this resource."

    let rangeNotSatisfiable = showErrorPage Http.RangeNotSatisfiable "Range Not Satisfiable" "The requested byte range was not valid."

    let methodNotAllowed = showErrorPage Http.MethodNotAllowed "Method Not Allowed" "The supplied method is not allowed for this resource"
        

