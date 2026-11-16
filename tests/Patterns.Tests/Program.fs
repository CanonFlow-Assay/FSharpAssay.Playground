module Playground.PatternTests.Program

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Playground.Patterns
open Playground.Pipeline

[<Tests>]
let tests = testList "Functional crosswalk and bounded effects" [
    testCase "quantity exact lower bound" <| fun _ -> Expect.isOk (Quantity.create 1) "one is valid"
    testCase "quantity exact upper bound" <| fun _ -> Expect.isOk (Quantity.create 99) "99 is valid"
    testCase "quantity outside bounds" <| fun _ -> for n in [Int32.MinValue;0;100;Int32.MaxValue] do Expect.isError (Quantity.create n) "bounds"
    testCase "independent validation accumulates errors" <| fun _ -> Expect.equal (Order.create "" 0) (Error [EmptyCustomer;InvalidQuantity]) "both fields diagnosed"
    testCase "function capability produces a value and audit data" <| fun _ ->
        let actual=Workflow.execute (fun q -> decimal(Quantity.value q)*2m) "Ada" 3
        Expect.equal actual (Ok(6m,["input-validated";"price-calculated"])) "no logger effect"
    testCase "shipment DU handles both alternatives" <| fun _ ->
        Expect.equal (Shipping.fee(Pickup "A")) 0m "pickup"
        Expect.equal (Shipping.fee(Delivery("A",2m<kg>))) 9m "measured weight"
    testCase "recursive menu returns all links in order" <| fun _ ->
        let menu=Group("root",[Link("one","/1");Group("inner",[Link("two","/2")])])
        Expect.equal (Menu.links menu) ["one","/1";"two","/2"] "recursive data traversal"
    testCase "ledger replay is deterministic" <| fun _ ->
        Expect.equal (Ledger.replay [Credited 10m;Debited 3m;Credited 2m]) 9m "events fold"
    testCase "ledger rejects overdraft and permits exact balance" <| fun _ ->
        Expect.isError (Ledger.decide 11m 10m) "overdraft"
        Expect.equal (Ledger.decide 10m 10m) (Ok(Debited 10m)) "inclusive boundary"
    testCase "active pattern rejects malformed and negative input" <| fun _ ->
        for input in ["";"bad";"-1";"0"] do Expect.isError (Parsing.positive input) "boundary parser"
        Expect.equal (Parsing.positive "12") (Ok 12) "valid input"
    testCaseAsync "bounded pipeline preserves order" <| async {
        let work n (token: CancellationToken) = task {
            do! Task.Delay((11-n),token)
            return n*2
        }
        let! result = Bounded.map 2 work [1..10] CancellationToken.None |> Async.AwaitTask
        Expect.sequenceEqual result ([1..10] |> List.map ((*)2)) "completion order does not change output order"
    }
    testCaseAsync "pipeline uses at most its declared workers" <| async {
        let gate=TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let started=TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)
        let running=ref 0
        let maximum=ref 0
        let work n (token: CancellationToken) = task {
            let current=Interlocked.Increment(running)
            if current>maximum.Value then maximum.Value<-current
            if current=2 then started.TrySetResult() |> ignore
            do! gate.Task.WaitAsync(token)
            Interlocked.Decrement(running) |> ignore
            return n
        }
        let execution=Bounded.map 2 work [1..20] CancellationToken.None
        do! started.Task.WaitAsync(TimeSpan.FromSeconds 5.) |> Async.AwaitTask
        Expect.equal running.Value 2 "two workers admitted"
        gate.SetResult()
        let! result=execution |> Async.AwaitTask
        Expect.isLessThanOrEqual maximum.Value 2 "fanout bound"
        Expect.equal result.Length 20 "every item completed"
    }
    testCaseAsync "consumer failure unblocks a saturated producer" <| async {
        let work n (_: CancellationToken) = task {
            if n=2 then raise (InvalidOperationException("fixture failure"))
            return n
        }
        let execution=Bounded.map 1 work [1..10000] CancellationToken.None
        let guarded=task {
            try
                let! _=execution.WaitAsync(TimeSpan.FromSeconds 5.)
                return false
            with :? InvalidOperationException as error -> return error.Message="fixture failure"
        }
        let! failed=guarded |> Async.AwaitTask
        Expect.isTrue failed "failure is observed without a blocked writer"
    }
    testCaseAsync "cancellation terminates owned work" <| async {
        use cancel=new CancellationTokenSource()
        let work n (_: CancellationToken) = task {
            cancel.Cancel()
            cancel.Token.ThrowIfCancellationRequested()
            return n
        }
        let execution=Bounded.map 1 work [1..10000] cancel.Token
        let! failed=async {try let! _=execution.WaitAsync(TimeSpan.FromSeconds 5.) |> Async.AwaitTask in return false with :? OperationCanceledException -> return true}
        Expect.isTrue failed "cancellation stays cancellation"
    }
]
