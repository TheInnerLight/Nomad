namespace Nomad

open System.Threading.Tasks

module Async =
    let inline return' x = async.Return x

    let inline bind f x = async.Bind(x, f)

    let map f x = async.Bind(x, async.Return << f)

    let apply f x = async.Bind(f, fun fe -> map fe x)

    let inline startAsPlainTask (work : Async<unit>) : System.Threading.Tasks.Task = 
        Async
            .StartAsTask(work)
            .ContinueWith(System.Action<Task>(fun _ -> ()))

    let inline startAsPlainTaskWithCancellation cancellationToken (work : Async<unit>) : System.Threading.Tasks.Task = 
        Async
            .StartAsTask(work, cancellationToken = cancellationToken)
            .ContinueWith(System.Action<Task>(fun _ -> ()))

    let inline awaitPlainTask (task : Task) =
        task.ContinueWith(fun t -> ())
        |> Async.AwaitTask
