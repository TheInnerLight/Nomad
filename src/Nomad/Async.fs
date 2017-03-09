namespace Nomad

module Async =
    let inline return' x = async.Return x

    let inline bind x f = async.Bind(x, f)

    let map f x = async.Bind(x, async.Return << f)

    let inline startAsPlainTask (work : Async<unit>) : System.Threading.Tasks.Task = 
        
        System.Threading.Tasks.Task.Factory.StartNew(fun () -> work |> Async.RunSynchronously)

    let inline startAsPlainTaskWithCancellation cancellationToken (work : Async<unit>) : System.Threading.Tasks.Task = 
        Async
            .StartAsTask(work, cancellationToken = cancellationToken)
            .ContinueWith(System.Action<System.Threading.Tasks.Task<unit>>(ignore))