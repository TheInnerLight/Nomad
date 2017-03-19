namespace Nomad

open HttpHandler

/// A set of generic template responses
module Responses =
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

    let showErrorPage status title desc =
        setStatus status
        *> setContentType ContentType.``text/html``
        *> writeText (errorPage (Http.responseCode status) title desc)

    let ``Not Found`` = showErrorPage Http.NotFound "Not Found" "The resource that you requested was not found."

    let ``Forbidden`` = showErrorPage Http.Forbidden "Forbidden" "You do not have permission to access this resource."
        

