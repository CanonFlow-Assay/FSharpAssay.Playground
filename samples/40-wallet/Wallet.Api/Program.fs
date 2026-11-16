module Playground.Wallet.Api.Program

open System
open System.Net
open System.Threading
open Playground.Wallet.Adapter

[<EntryPoint>]
let main _ =
    let connection=Environment.GetEnvironmentVariable("FSPLAYGROUND_POSTGRES")
    let token=Environment.GetEnvironmentVariable("FSPLAYGROUND_TOKEN")
    if String.IsNullOrWhiteSpace connection || String.IsNullOrWhiteSpace token then
        eprintfn "Set FSPLAYGROUND_POSTGRES and FSPLAYGROUND_TOKEN for a dedicated fixture database."
        64
    else
        let store=Store(connection)
        use listener=new HttpListener()
        listener.Prefixes.Add("http://127.0.0.1:5088/")
        listener.Start()
        use cancel=new CancellationTokenSource()
        Console.CancelKeyPress.Add(fun event -> event.Cancel<-true;cancel.Cancel())
        try
            while not cancel.IsCancellationRequested do
                Http.serveOnce listener store token 1000m cancel.Token |> _.GetAwaiter().GetResult()
            0
        with :? OperationCanceledException -> 0
