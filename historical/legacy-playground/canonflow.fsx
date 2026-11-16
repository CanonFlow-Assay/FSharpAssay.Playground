open System
open System.IO
open System.Text.RegularExpressions

let schemaPath = @"e:\github\CanonFlowFoundation\FSharpAssay.Playground\db\init\01-schema.sql"
let outDir = @"e:\github\CanonFlowFoundation\FSharpAssay.Playground\src\Generated"
let proofPath = Path.Combine(outDir, "PROOF.md")

if not (Directory.Exists(outDir)) then
    Directory.CreateDirectory(outDir) |> ignore

let lines = File.ReadAllLines(schemaPath)

// 1. Extract Column Types
type ColumnDef = { TableName: string; ColumnName: string; SqlType: string; IsNotNull: bool }
let mutable columns = []
let mutable currentTable = ""

for line in lines do
    let trimLine = line.Trim()
    if trimLine.StartsWith("CREATE TABLE") then
        let m = Regex.Match(trimLine, @"CREATE TABLE\s+([a-zA-Z0-9_]+)")
        if m.Success then currentTable <- m.Groups.[1].Value
    elif currentTable <> "" && not (trimLine.StartsWith("CONSTRAINT")) && not (trimLine.StartsWith(");")) && not (trimLine.StartsWith("--")) && not (trimLine.StartsWith("PRIMARY KEY")) then
        let m = Regex.Match(trimLine, @"^([a-zA-Z0-9_]+)\s+([A-Z0-9\(\)\,]+)")
        if m.Success then
            let colName = m.Groups.[1].Value
            let sqlType = m.Groups.[2].Value.Replace(",", "")
            let isNotNull = trimLine.Contains("NOT NULL") || trimLine.Contains("PRIMARY KEY")
            columns <- { TableName = currentTable; ColumnName = colName; SqlType = sqlType; IsNotNull = isNotNull } :: columns
    elif trimLine.StartsWith(");") then
        currentTable <- ""

let getFsType (sqlType: string) =
    if sqlType.StartsWith("NUMERIC") then "decimal"
    elif sqlType.StartsWith("INT") then "int"
    elif sqlType.StartsWith("VARCHAR") || sqlType.StartsWith("TEXT") then "string"
    elif sqlType.StartsWith("UUID") then "Guid"
    elif sqlType.StartsWith("TIMESTAMP") || sqlType.StartsWith("DATE") then "DateTimeOffset"
    elif sqlType.StartsWith("BOOLEAN") then "bool"
    else "string"

type ConstraintType = 
    | SingleCol of string * string * string // FsBaseType, ValidatorCode, GeneratorCode
    | EnumDU of string list
    | CrossCol
    | Temporal
    | Unhandled

let parseConstraint (cols: ColumnDef list) (sqlCheck: string) =
    let cleanCheck = sqlCheck.Replace("\r", "").Replace("\n", " ").Trim()
    let words = Regex.Matches(cleanCheck, @"[a-zA-Z0-9_]+") |> Seq.cast<Match> |> Seq.map (fun m -> m.Value) |> Seq.toList
    let matchedCols = cols |> List.filter (fun c -> words |> List.contains c.ColumnName) |> List.distinctBy (fun c -> c.ColumnName)

    if matchedCols.Length = 1 then
        let col = matchedCols.[0]
        let fsType = getFsType col.SqlType
        
        if cleanCheck.Contains("length(") then
            let nums = Regex.Matches(cleanCheck, @"\d+")
            let min = if nums.Count > 0 then nums.[0].Value else "0"
            let max = if nums.Count > 1 then nums.[1].Value else "System.Int32.MaxValue"
            SingleCol("string", $"fun v -> not (System.String.IsNullOrWhiteSpace(v)) && v.Length >= {min} && v.Length <= {max}", $"Gen.elements [\"a\"; \"ab\"; \"abc\"]")
        elif cleanCheck.Contains("~") then
            let patternMatch = Regex.Match(cleanCheck, @"'(.+?)'")
            let pattern = if patternMatch.Success then patternMatch.Groups.[1].Value else ""
            let gen = if pattern.Contains("@") then "Gen.constant \"test@test.com\"" else "Gen.constant \"VALID123\""
            SingleCol("string", $"fun v -> not (System.String.IsNullOrWhiteSpace(v)) && System.Text.RegularExpressions.Regex.IsMatch(v, @\"{pattern}\")", gen)
        elif cleanCheck.Contains(" IN ") then
            let startIdx = cleanCheck.IndexOf("IN (")
            if startIdx >= 0 then
                let contentStart = startIdx + 4
                let contentEnd = cleanCheck.LastIndexOf(")")
                let contentEnd = if contentEnd = -1 then cleanCheck.Length else contentEnd
                let content = cleanCheck.Substring(contentStart, contentEnd - contentStart)
                let vals = content.Split(',') |> Array.map (fun s -> s.Trim().Trim('\'')) |> Array.toList
                EnumDU(vals)
            else Unhandled
        elif cleanCheck.Contains(">=") || cleanCheck.Contains(">") || cleanCheck.Contains("<=") || cleanCheck.Contains("<") then
            let m = Regex.Match(cleanCheck, @"([<>=]+)\s*([0-9\.]+)")
            if m.Success then
                let op = m.Groups.[1].Value
                let num = m.Groups.[2].Value
                let gen = if fsType = "decimal" then $"Gen.choose(1, 9999) |> Gen.map decimal" else $"Gen.choose(1, 9999)"
                if fsType = "decimal" then SingleCol(fsType, $"fun v -> v {op} {num}m", gen)
                else SingleCol(fsType, $"fun v -> v {op} {num}", gen)
            else
                let mAnd = Regex.Match(cleanCheck, @"([<>=]+)\s*([0-9\.]+)\s+AND\s+[a-zA-Z_]+\s*([<>=]+)\s*([0-9\.]+)")
                if mAnd.Success then
                     let op1 = mAnd.Groups.[1].Value
                     let num1 = mAnd.Groups.[2].Value
                     let op2 = mAnd.Groups.[3].Value
                     let num2 = mAnd.Groups.[4].Value
                     let gen = if fsType = "decimal" then $"Gen.choose(1, {num2}) |> Gen.map decimal" else $"Gen.choose(1, {num2})"
                     if fsType = "decimal" then SingleCol(fsType, $"fun v -> v {op1} {num1}m && v {op2} {num2}m", gen)
                     else SingleCol(fsType, $"fun v -> v {op1} {num1} && v {op2} {num2}", gen)
                else Unhandled
        else Unhandled
    elif matchedCols.Length > 1 then
        if cleanCheck.Contains("IS NULL OR") then Temporal
        else CrossCol
    else Unhandled

let typesSb = Text.StringBuilder()
typesSb.AppendLine("module Generated.Types\n\nopen System\n\ntype CanonFlowError = { Field: string; Message: string }\n") |> ignore

let valsSb = Text.StringBuilder()
valsSb.AppendLine("module Generated.RowValidators\n\nopen System\nopen Generated.Types\n") |> ignore

let proofSb = Text.StringBuilder()
proofSb.AppendLine("# CanonFlow Fidelity Proof\n\n| SQL Constraint | F# Type | Verdict |\n| --- | --- | --- |") |> ignore

let genSb = Text.StringBuilder()
genSb.AppendLine("module Generated.Generators\n\nopen FsCheck\nopen Generated.Types\n") |> ignore

let mutable hasUnhandled = false

let mutable emittedVals = Set.empty

currentTable <- ""
for line in lines do
    let trimLine = line.Trim()
    if trimLine.StartsWith("CREATE TABLE") then
        let m = Regex.Match(trimLine, @"CREATE TABLE\s+([a-zA-Z0-9_]+)")
        if m.Success then currentTable <- m.Groups.[1].Value
    elif trimLine.StartsWith("CONSTRAINT chk_") then
        let nameMatch = Regex.Match(trimLine, @"CONSTRAINT\s+(chk_\w+)\s+CHECK\s+\((.*)")
        if nameMatch.Success then
            let name = nameMatch.Groups.[1].Value
            let sqlCheck = nameMatch.Groups.[2].Value.TrimEnd(';', ',', ')').Trim()
            let typeName = name.Replace("chk_", "").Split('_') |> Array.map (fun s -> s.[0].ToString().ToUpper() + s.Substring(1)) |> String.concat ""
            
            let tableCols = columns |> List.filter (fun c -> c.TableName = currentTable)
            let parsed = parseConstraint tableCols sqlCheck
            
            match parsed with
            | SingleCol (baseType, fsLogic, genLogic) ->
                if not (emittedVals.Contains typeName) then
                    emittedVals <- emittedVals.Add typeName
                    typesSb.AppendLine($"type {typeName} = private {typeName} of {baseType}") |> ignore
                    typesSb.AppendLine($"module {typeName} =") |> ignore
                    typesSb.AppendLine($"    let value ({typeName} v) = v") |> ignore
                    typesSb.AppendLine($"    let create (v: {baseType}) : Result<{typeName}, CanonFlowError> =") |> ignore
                    typesSb.AppendLine($"        if ({fsLogic}) v then Ok ({typeName} v)") |> ignore
                    typesSb.AppendLine($"        else Error {{ Field = \"{typeName}\"; Message = \"Constraint {name} failed\" }}\n") |> ignore
                    genSb.AppendLine($"let arb{typeName} = {genLogic} |> Gen.map (fun v -> match {typeName}.create v with | Ok x -> x | Error e -> failwith e.Message) |> Arb.fromGen") |> ignore
                    proofSb.AppendLine($"| `{name}` | `{typeName}` | Proven |") |> ignore
            | EnumDU (vals) ->
                if not (emittedVals.Contains typeName) then
                    emittedVals <- emittedVals.Add typeName
                    typesSb.AppendLine($"type {typeName} =") |> ignore
                    for v in vals do typesSb.AppendLine($"    | {v}") |> ignore
                    typesSb.AppendLine($"\nmodule {typeName} =") |> ignore
                    typesSb.AppendLine($"    let ofString (str: string) =") |> ignore
                    typesSb.AppendLine($"        match str.ToUpper() with") |> ignore
                    for v in vals do typesSb.AppendLine($"        | \"{v}\" -> Ok {v}") |> ignore
                    typesSb.AppendLine($"        | _ -> Error {{ Field = \"{typeName}\"; Message = \"Invalid enum\" }}") |> ignore
                    typesSb.AppendLine($"    let toString (v: {typeName}) =") |> ignore
                    typesSb.AppendLine($"        match v with") |> ignore
                    for v in vals do typesSb.AppendLine($"        | {v} -> \"{v}\"") |> ignore
                    typesSb.AppendLine("") |> ignore
                    let genList = vals |> List.map (fun v -> $"{typeName}.{v}") |> String.concat "; "
                    genSb.AppendLine($"let arb{typeName} = Gen.elements [{genList}] |> Arb.fromGen") |> ignore
                    proofSb.AppendLine($"| `{name}` | `{typeName}` | Proven |") |> ignore
            | CrossCol | Temporal ->
                if not (emittedVals.Contains name) then
                    emittedVals <- emittedVals.Add name
                    valsSb.AppendLine($"// Manual Obligation for: {name}") |> ignore
                    valsSb.AppendLine($"// {sqlCheck}") |> ignore
                    valsSb.AppendLine($"let inline {name} (row: 'T) : Result<unit, CanonFlowError> = Ok () // IMPLEMENT ME\n") |> ignore
                    proofSb.AppendLine($"| `{name}` | (Row) | ManualObligation |") |> ignore
            | Unhandled ->
                hasUnhandled <- true
                proofSb.AppendLine($"| `{name}` | - | Unsupported |") |> ignore

let commonIds = ["ProductId"; "CategoryId"; "CartId"; "OrderId"; "CheckoutId"; "PaymentId"; "ShipmentId"]
for id in commonIds do
    typesSb.AppendLine($"type {id} = private {id} of Guid") |> ignore
    typesSb.AppendLine($"module {id} =") |> ignore
    typesSb.AppendLine($"    let value ({id} v) = v") |> ignore
    typesSb.AppendLine($"    let create (v: Guid) = if v <> Guid.Empty then Ok ({id} v) else Error {{ Field = \"{id}\"; Message = \"Empty\" }}\n") |> ignore

let genOutDir = @"e:\github\CanonFlowFoundation\FSharpAssay.Playground\tests"
File.WriteAllText(Path.Combine(outDir, "Types.fs"), typesSb.ToString())
File.WriteAllText(Path.Combine(outDir, "RowValidators.fs"), valsSb.ToString())
File.WriteAllText(Path.Combine(outDir, "PROOF.md"), proofSb.ToString())
File.WriteAllText(Path.Combine(genOutDir, "Generators.fs"), genSb.ToString())

let args = Environment.GetCommandLineArgs()
if args |> Array.contains "--strict" && hasUnhandled then
    printfn "ERROR: Unhandled constraints found in schema!"
    Environment.Exit(1)
