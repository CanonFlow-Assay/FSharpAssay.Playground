namespace Playground.Wallet.Adapter

open System
open System.Net
open System.IO
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Npgsql
open Playground.Wallet

module Wire =
    let encode = function
        | Accepted(reference,balance) -> JsonSerializer.Serialize {|reference=reference;status="accepted";balance=balance|}
        | InsufficientFunds reference -> JsonSerializer.Serialize {|reference=reference;status="insufficient-funds";balance=0m|}
        | Conflict reference -> JsonSerializer.Serialize {|reference=reference;status="conflict";balance=0m|}

type Store(connectionString: string) =
    let connect () =
        let connection=new NpgsqlConnection(connectionString)
        connection.Open()
        connection
    let command connection transaction text =
        let cmd=new NpgsqlCommand(text,connection,transaction)
        cmd.CommandTimeout<-5
        cmd
    do
        let parsed=NpgsqlConnectionStringBuilder(connectionString)
        if not (parsed.Database.StartsWith("fsplayground_",StringComparison.Ordinal)) then
            invalidArg "connectionString" "the fixture requires a dedicated fsplayground_ database"
        use connection=connect()
        use cmd=command connection null "CREATE TABLE IF NOT EXISTS wallets(id text PRIMARY KEY,balance numeric(18,2) NOT NULL CHECK(balance>=0)); CREATE TABLE IF NOT EXISTS operations(reference text PRIMARY KEY,wallet text NOT NULL,amount numeric(18,2) NOT NULL CHECK(amount>0),status text NOT NULL,balance numeric(18,2) NOT NULL); CREATE TABLE IF NOT EXISTS outbox(reference text PRIMARY KEY REFERENCES operations(reference),event text NOT NULL);"
        cmd.ExecuteNonQuery() |> ignore
    member _.Reset(balance: decimal) =
        use connection=connect()
        use cmd=command connection null "TRUNCATE outbox,operations,wallets; INSERT INTO wallets VALUES('a',@balance),('b',@balance)"
        cmd.Parameters.AddWithValue("balance",balance) |> ignore
        cmd.ExecuteNonQuery() |> ignore
    member _.Debit(wallet: string, debit: Debit, cancellation: CancellationToken) =
        cancellation.ThrowIfCancellationRequested()
        use connection=connect()
        use transaction=connection.BeginTransaction()
        let reference=Debit.reference debit
        let amount=Debit.amount debit
        // Serialize only equal idempotency keys; wallet UPDATE handles competing keys.
        use lock=command connection transaction "SELECT pg_advisory_xact_lock(hashtextextended(@reference,0))"
        lock.Parameters.AddWithValue("reference",reference) |> ignore
        lock.ExecuteNonQuery() |> ignore
        use query=command connection transaction "SELECT wallet,amount,status,balance FROM operations WHERE reference=@reference"
        query.Parameters.AddWithValue("reference",reference) |> ignore
        use reader=query.ExecuteReader()
        let prior=if reader.Read() then Some(reader.GetString(0),reader.GetDecimal(1),reader.GetString(2),reader.GetDecimal(3)) else None
        reader.Close()
        match prior with
        | Some(previousWallet,previousAmount,status,balance) when previousWallet=wallet && previousAmount=amount ->
            if status="accepted" then Accepted(reference,balance) else InsufficientFunds reference
        | Some _ -> Conflict reference
        | None ->
            cancellation.ThrowIfCancellationRequested()
            use update=command connection transaction "UPDATE wallets SET balance=balance-@amount WHERE id=@wallet AND balance>=@amount RETURNING balance"
            update.Parameters.AddWithValue("amount",amount) |> ignore
            update.Parameters.AddWithValue("wallet",wallet) |> ignore
            let updated=update.ExecuteScalar()
            let result=if isNull updated then InsufficientFunds reference else Accepted(reference,Convert.ToDecimal updated)
            let status,balance=match result with Accepted(_,b) -> "accepted",b | _ -> "insufficient-funds",0m
            use insert=command connection transaction "INSERT INTO operations(reference,wallet,amount,status,balance) VALUES(@reference,@wallet,@amount,@status,@balance)"
            for key,value in ["reference",box reference;"wallet",box wallet;"amount",box amount;"status",box status;"balance",box balance] do insert.Parameters.AddWithValue(key,value) |> ignore
            insert.ExecuteNonQuery() |> ignore
            match result with
            | Accepted _ ->
                use event=command connection transaction "INSERT INTO outbox VALUES(@reference,'debited')"
                event.Parameters.AddWithValue("reference",reference) |> ignore
                event.ExecuteNonQuery() |> ignore
            | _ -> ()
            cancellation.ThrowIfCancellationRequested()
            transaction.Commit()
            result
    member _.Balance(wallet: string) =
        use connection=connect()
        use query=command connection null "SELECT balance FROM wallets WHERE id=@wallet"
        query.Parameters.AddWithValue("wallet",wallet) |> ignore
        Convert.ToDecimal(query.ExecuteScalar())
    member _.Counts =
        use connection=connect()
        use query=command connection null "SELECT (SELECT count(*) FROM operations),(SELECT count(*) FROM outbox)"
        use reader=query.ExecuteReader()
        reader.Read() |> ignore
        reader.GetInt64(0),reader.GetInt64(1)

module Http =
    /// A bounded single-request fixture, also used by the small console host.
    let serveOnce (listener: HttpListener) (store: Store) (token: string) limit (cancellation: CancellationToken) = task {
        let! context=listener.GetContextAsync().WaitAsync(cancellation)
        let respond status text = task {
            let bytes=Encoding.UTF8.GetBytes(text: string)
            context.Response.StatusCode<-status
            context.Response.ContentType<-"application/json"
            context.Response.ContentLength64<-int64 bytes.Length
            do! context.Response.OutputStream.WriteAsync(bytes.AsMemory(),cancellation)
            context.Response.Close()
        }
        if context.Request.HttpMethod<>"POST" || context.Request.Url.AbsolutePath<>"/debit" then
            do! respond 404 "{\"error\":\"not-found\"}"
        elif String.IsNullOrWhiteSpace token || context.Request.Headers.["Authorization"]<>"Bearer "+token then
            do! respond 401 "{\"error\":\"unauthorized\"}"
        else
            let! parsed=task {
                try
                    use body=new MemoryStream()
                    let buffer=Array.zeroCreate<byte> 1024
                    let mutable more=true
                    while more && body.Length<=8192L do
                        let! count=context.Request.InputStream.ReadAsync(buffer.AsMemory(),cancellation)
                        if count=0 then more<-false else body.Write(buffer,0,count)
                    if body.Length>8192L then return Error(413,"{\"error\":\"body-too-large\"}")
                    else
                        use json=JsonDocument.Parse(body.ToArray())
                        let reference=json.RootElement.GetProperty("reference").GetString()
                        let amount=json.RootElement.GetProperty("amount").GetDecimal()
                        let wallet=json.RootElement.GetProperty("wallet").GetString()
                        if String.IsNullOrWhiteSpace wallet then return Error(400,"{\"error\":\"wallet-required\"}")
                        else
                            match Debit.create limit reference amount with
                            | Error _ -> return Error(400,"{\"error\":\"invalid-debit\"}")
                            | Ok debit -> return Ok(wallet,debit)
                with
                | :? JsonException | :? InvalidOperationException | :? FormatException | :? OverflowException | :? Collections.Generic.KeyNotFoundException ->
                    return Error(400,"{\"error\":\"invalid-request\"}")
            }
            match parsed with
            | Error(status,text) -> do! respond status text
            | Ok(wallet,debit) ->
                // Datastore failures must propagate rather than masquerade as bad JSON.
                let outcome=store.Debit(wallet,debit,cancellation)
                do! respond (match outcome with Conflict _ -> 409 | _ -> 200) (Wire.encode outcome)
    }
