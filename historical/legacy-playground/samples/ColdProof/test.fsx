#r "nuget: pgsqlparser"
#r "nuget: Google.Protobuf"
open PgSqlParser
let result = Parser.Parse("SELECT -80;")
let json = Google.Protobuf.JsonFormatter.Default.Format(result.Value)
printfn "%s" json
