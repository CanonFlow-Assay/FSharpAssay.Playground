namespace Playground.Pipeline

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks

module Bounded =
    /// Bounds queued/running work; the final ordered result array retains all outputs.
    let map concurrency (work: 'a -> CancellationToken -> Task<'b>) (source: seq<'a>) (cancellation: CancellationToken) = task {
        if concurrency < 1 || concurrency > 64 then invalidArg "concurrency" "choose 1..64 workers"
        use linked = CancellationTokenSource.CreateLinkedTokenSource cancellation
        let options = BoundedChannelOptions(concurrency, FullMode=BoundedChannelFullMode.Wait, SingleWriter=true)
        let queue = Channel.CreateBounded<int * 'a>(options)
        let results = ConcurrentDictionary<int,'b>()
        let mutable count = 0
        let producer = task {
            try
                for item in source do
                    do! queue.Writer.WriteAsync((count,item),linked.Token).AsTask()
                    count <- count+1
                queue.Writer.TryComplete() |> ignore
            with error ->
                queue.Writer.TryComplete(error) |> ignore
                linked.Cancel()
                return raise error
        }
        let consumer () = task {
            try
                let mutable more = true
                while more do
                    let! available = queue.Reader.WaitToReadAsync(linked.Token).AsTask()
                    if not available then more <- false
                    else
                        let mutable pair = Unchecked.defaultof<int * 'a>
                        while queue.Reader.TryRead(&pair) do
                            let index,item = pair
                            let! result = work item linked.Token
                            results.[index] <- result
            with error ->
                linked.Cancel()
                return raise error
        }
        let workers = Array.init concurrency (fun _ -> consumer() :> Task)
        do! Task.WhenAll(Array.append [|producer :> Task|] workers)
        cancellation.ThrowIfCancellationRequested()
        return Array.init count (fun index -> results.[index])
    }
