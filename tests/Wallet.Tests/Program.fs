module Playground.WalletTests.Program

open System
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Expecto
open Playground.Wallet
open Playground.Wallet.Adapter

let value = function Ok x -> x | Error e -> failwithf "fixture error %A" e
let store () =
    let connection=Environment.GetEnvironmentVariable("FSPLAYGROUND_POSTGRES")
    if String.IsNullOrWhiteSpace connection then failwith "real PostgreSQL fixture is required; no test is skipped"
    let result=Store(connection)
    result.Reset(10m)
    result
let debit reference amount = Debit.create 1000m reference amount |> value
let execute (store: Store) reference amount = store.Debit("a",debit reference amount,CancellationToken.None)

let request token body = async {
    let database=store()
    use reservation=new TcpListener(IPAddress.Loopback,0)
    reservation.Start()
    let port=(reservation.LocalEndpoint :?> IPEndPoint).Port
    reservation.Stop()
    use listener=new HttpListener()
    listener.Prefixes.Add(sprintf "http://127.0.0.1:%d/" port)
    listener.Start()
    use timeout=new CancellationTokenSource(TimeSpan.FromSeconds 10.)
    let serving=Http.serveOnce listener database "fixture-token" 1000m timeout.Token
    use client=new HttpClient(Timeout=TimeSpan.FromSeconds 5.)
    use message=new HttpRequestMessage(HttpMethod.Post,sprintf "http://127.0.0.1:%d/debit" port)
    if token then message.Headers.Add("Authorization","Bearer fixture-token")
    message.Content<-new StringContent(body,Encoding.UTF8,"application/json")
    let! response=client.SendAsync(message,timeout.Token) |> Async.AwaitTask
    use response=response
    let! text=response.Content.ReadAsStringAsync(timeout.Token) |> Async.AwaitTask
    do! serving |> Async.AwaitTask
    return int response.StatusCode,text,database.Balance("a"),database.Counts
}

[<Tests>]
let tests = testSequenced <| testList "Real wallet PostgreSQL and HTTP" [
    testCase "negative amount rejected by domain" <| fun _ -> Expect.isError (Debit.create 1000m "A" -1m) "positive invariant"
    testCase "zero amount rejected by domain" <| fun _ -> Expect.isError (Debit.create 1000m "A" 0m) "positive invariant"
    testCase "blank reference rejected" <| fun _ -> Expect.isError (Debit.create 1000m " " 1m) "reference required"
    testCase "overlong reference rejected" <| fun _ -> Expect.isError (Debit.create 1000m (String.replicate 65 "a") 1m) "bounded key"
    testCase "sub-cent amount rejected" <| fun _ -> Expect.isError (Debit.create 1000m "A" 1.001m) "money precision"
    testCase "exact request limit accepted" <| fun _ -> Expect.isOk (Debit.create 1000m "A" 1000m) "inclusive request boundary"
    testCase "above request limit rejected" <| fun _ -> Expect.isError (Debit.create 1000m "A" 1000.01m) "bounded request"
    testCase "reference normalization is explicit" <| fun _ -> Expect.equal (Debit.reference(debit " A " 1m)) "A" "trim input"
    testCase "real debit persists reduced balance" <| fun _ ->
        let database=store()
        Expect.equal (execute database "A" 3m) (Accepted("A",7m)) "accepted decision"
        Expect.equal (database.Balance("a")) 7m "database state"
    testCase "exact-balance debit leaves zero" <| fun _ ->
        let database=store()
        Expect.equal (execute database "A" 10m) (Accepted("A",0m)) "funds boundary"
        Expect.equal (database.Balance("a")) 0m "no negative balance"
    testCase "insufficient funds retain state and no outbox event" <| fun _ ->
        let database=store()
        Expect.equal (execute database "A" 11m) (InsufficientFunds "A") "explicit rejection"
        Expect.equal (database.Balance("a")) 10m "no debit"
        Expect.equal database.Counts (1L,0L) "rejected response retained without event"
    testCase "identical replay returns original response once" <| fun _ ->
        let database=store()
        let first=execute database "A" 3m
        Expect.equal (execute database "A" 3m) first "exact replay"
        Expect.equal (database.Balance("a")) 7m "only one debit"
        Expect.equal database.Counts (1L,1L) "one operation and one event"
    testCase "changed replay amount conflicts without another write" <| fun _ ->
        let database=store()
        execute database "A" 3m |> ignore
        Expect.equal (execute database "A" 4m) (Conflict "A") "changed intent"
        Expect.equal (database.Balance("a")) 7m "no second debit"
        Expect.equal database.Counts (1L,1L) "outbox not duplicated"
    testCase "changed replay wallet conflicts" <| fun _ ->
        let database=store()
        execute database "A" 3m |> ignore
        Expect.equal (database.Debit("b",debit "A" 3m,CancellationToken.None)) (Conflict "A") "wallet is part of intent"
        Expect.equal (database.Balance("b")) 10m "other wallet untouched"
    testCase "cancelled write changes no rows" <| fun _ ->
        let database=store()
        use token=new CancellationTokenSource()
        token.Cancel()
        Expect.throws (fun () -> database.Debit("a",debit "A" 1m,token.Token) |> ignore) "cancellation propagates"
        Expect.equal database.Counts (0L,0L) "no effects"
    testCaseAsync "concurrent distinct requests cannot overspend" <| async {
        let database=store()
        let operations=[|for i in 1..12 -> Task.Run(fun () -> execute database (string i) 1m)|]
        let! results=Task.WhenAll(operations).WaitAsync(TimeSpan.FromSeconds 15.) |> Async.AwaitTask
        let accepted=results |> Array.filter (function Accepted _ -> true | _ -> false) |> Array.length
        Expect.equal accepted 10 "only available funds spent"
        Expect.equal (database.Balance("a")) 0m "concurrent invariant"
        Expect.equal database.Counts (12L,10L) "every result retained and only accepted events published"
    }
    testCaseAsync "concurrent identical requests debit once" <| async {
        let database=store()
        let! results=Task.WhenAll([|for _ in 1..8 -> Task.Run(fun () -> execute database "same" 3m)|]).WaitAsync(TimeSpan.FromSeconds 15.) |> Async.AwaitTask
        Expect.isTrue (results |> Array.forall ((=) (Accepted("same",7m)))) "same replay response"
        Expect.equal database.Counts (1L,1L) "single effect"
        Expect.equal (database.Balance("a")) 7m "one subtraction"
    }
    testCaseAsync "HTTP rejects unauthenticated request before persistence" <| async {
        let! status,_,balance,counts=request false "{\"reference\":\"A\",\"wallet\":\"a\",\"amount\":3}"
        Expect.equal (status,balance,counts) (401,10m,(0L,0L)) "auth boundary"
    }
    testCaseAsync "HTTP invalid amount returns 400 without writes" <| async {
        let! status,_,balance,counts=request true "{\"reference\":\"A\",\"wallet\":\"a\",\"amount\":-3}"
        Expect.equal (status,balance,counts) (400,10m,(0L,0L)) "validation boundary"
    }
    testCaseAsync "HTTP returns actual accepted wire contract" <| async {
        let! status,text,balance,counts=request true "{\"reference\":\"A\",\"wallet\":\"a\",\"amount\":3}"
        use json=JsonDocument.Parse(text)
        Expect.equal status 200 "HTTP success"
        Expect.sequenceEqual (json.RootElement.EnumerateObject() |> Seq.map _.Name |> Seq.sort) ["balance";"reference";"status"] "registered field contract"
        Expect.equal (json.RootElement.GetProperty("reference").GetString()) "A" "id retained"
        Expect.equal (json.RootElement.GetProperty("balance").GetDecimal()) 7m "state in response"
        Expect.equal (balance,counts) (7m,(1L,1L)) "committed result"
    }
    testCaseAsync "HTTP bounds request body" <| async {
        let! status,_,balance,counts=request true (String.replicate 9000 "x")
        Expect.equal (status,balance,counts) (413,10m,(0L,0L)) "bounded input"
    }
    testCaseAsync "HTTP malformed JSON returns bounded error" <| async {
        let! status,text,_,counts=request true "{malformed"
        use json=JsonDocument.Parse(text)
        Expect.equal status 400 "bad input"
        Expect.equal (json.RootElement.GetProperty("error").GetString()) "invalid-request" "no implementation details exposed"
        Expect.equal counts (0L,0L) "no writes"
    }
]
