module ColdProof.Dirty

open System
open System.IO
open Npgsql

type Shipment = {
    mutable Status: string  // Trap 1: Mutable
    Quantity: int
    Notes: string option
}

let connectionString = "Host=localhost;Username=postgres;Password=supersecret" // Trap 5: Secrets

let updateShipment (s: Shipment) (newStatus: string) =
    // Trap 7: Bad state transition (blind overwrite without checking old state)
    s.Status <- newStatus
    
    // Trap 2: Option.get
    let notes = s.Notes.Value 

    if s.Quantity < 0 then
        failwith "Invalid quantity" // Trap 3: failwith
        
    // Trap 4: I/O in domain
    File.WriteAllText("log.txt", $"Updated shipment to {newStatus}")

    // Trap 6: SQL injection
    let conn = new NpgsqlConnection(connectionString)
    conn.Open()
    use cmd = new NpgsqlCommand($"UPDATE shipments SET status = '{newStatus}' WHERE id = 1", conn)
    cmd.ExecuteNonQuery() |> ignore

// Trap 8: Dead code
let unusedFunction () =
    printfn "I am never called"
