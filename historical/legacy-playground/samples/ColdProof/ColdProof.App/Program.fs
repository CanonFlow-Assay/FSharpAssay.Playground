open System.IO
open System.Text.Json
open Canon.PgPrism
open Canon.Cli
open Canon.Core
open System
open PgSqlParser

module Mapper =
    let mapToProofTable (pgTable: Canon.PgPrism.TableDef) : Canon.Introspect.TableDef =
        let mapCol (pgCol: Canon.PgPrism.ColumnDef) =
            // Find constraints that apply to this column
            let colConstraints = 
                pgTable.Constraints
                |> List.choose (fun c -> 
                    match c.Expr with
                    | Lattice.Leaf (Constraint.FieldBound(f, _)) when f = pgCol.Name -> Some c.Expr
                    | Lattice.And(Lattice.Leaf(Constraint.FieldBound(f1, _)), _) when f1 = pgCol.Name -> Some c.Expr
                    | Lattice.Or(Lattice.Leaf(Constraint.FieldBound(f1, _)), _) when f1 = pgCol.Name -> Some c.Expr
                    | _ -> None
                )
            
            { Canon.Introspect.ColumnDef.Name = pgCol.Name
              Canon.Introspect.ColumnDef.DataType = sprintf "%A" pgCol.Type
              Canon.Introspect.ColumnDef.IsNullable = not pgCol.NotNull
              Canon.Introspect.ColumnDef.IsPrimaryKey = pgCol.PrimaryKey
              Canon.Introspect.ColumnDef.DefaultValue = None
              Canon.Introspect.ColumnDef.IsGenerated = false
              Canon.Introspect.ColumnDef.Description = None
              Canon.Introspect.ColumnDef.MaxLength = None
              Canon.Introspect.ColumnDef.CheckConstraints = []
              Canon.Introspect.ColumnDef.ParsedConstraints = colConstraints
              Canon.Introspect.ColumnDef.Semantics = None }
              
        { Canon.Introspect.TableDef.Schema = "public"
          Canon.Introspect.TableDef.Name = pgTable.Name
          Canon.Introspect.TableDef.Type = Canon.Introspect.TableType.Table
          Canon.Introspect.TableDef.Description = None
          Canon.Introspect.TableDef.Columns = pgTable.Columns |> List.map mapCol
          Canon.Introspect.TableDef.PrimaryKeys = []
          Canon.Introspect.TableDef.ForeignKeys = []
          Canon.Introspect.TableDef.Indexes = []
          Canon.Introspect.TableDef.TableConstraints = [] }

[<EntryPoint>]
let main argv =
    let sqlPath = Path.Combine(__SOURCE_DIRECTORY__, "..", "db", "init.sql")
    let sql = File.ReadAllText(sqlPath)

    printfn "Parsing SQL with PgSqlParser..."
    let result = Parser.Parse(sql)
    if not result.IsSuccess then
        printfn "Parse Error: %A" result.Error
        1
    else
        let jsonStr = Google.Protobuf.JsonFormatter.Default.Format(result.Value)
        use doc = JsonDocument.Parse(jsonStr)
        
        printfn "Decoding Schema with Canon.PgPrism..."
        let tables = Decode.decodeSchema doc.RootElement
        
        // Map to ProofEmitter input
        let proofTables = tables |> List.map Mapper.mapToProofTable
        
        printfn "Generating Fidelity Proofs..."
        let proofReport, approx, unsup = ProofEmitter.emitProofReport proofTables
        
        let evidencePath = Path.Combine(__SOURCE_DIRECTORY__, "..", "EVIDENCE.md")
        File.WriteAllText(evidencePath, proofReport)
        printfn $"Wrote evidence to {evidencePath}"
        
        if unsup > 0 then
            printfn $"WARNING: {unsup} constraints are Unsupported. See EVIDENCE.md."
            
        printfn "Emitting Domain.fs..."
        let domainCode = 
            tables 
            |> List.map Emit.generateFSharpDomain 
            |> String.concat "\n\n"
        
        let domainPath = Path.Combine(__SOURCE_DIRECTORY__, "..", "ColdProof.Tests", "Domain.fs")
        let domainContent = $"namespace ColdProof.Domain\n\nopen System\n\n{domainCode}"
        File.WriteAllText(domainPath, domainContent)
        printfn $"Wrote Domain.fs to {domainPath}"
        
        0
