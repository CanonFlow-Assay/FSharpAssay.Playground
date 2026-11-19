module Playground.DecisionAssist.Cli

open System
open System.IO
open System.Text.Json
open FsAssay.Playground.DecisionAssist

// System.Text.Json applies required-property checks to the boundary DTOs.
[<CLIMutable>]
type Candidate =
    { [<Serialization.JsonRequired>]
      id: string
      [<Serialization.JsonRequired>]
      status: string
      [<Serialization.JsonRequired>]
      passed: int
      [<Serialization.JsonRequired>]
      failed: int }

[<CLIMutable>]
type Execution =
    { [<Serialization.JsonRequired>]
      baselinePassed: int
      [<Serialization.JsonRequired>]
      snapshotMatches: bool
      [<Serialization.JsonRequired>]
      candidates: Candidate array }

let private evidence candidate =
    match candidate.status with
    | "Survived" when candidate.passed > 0 && candidate.failed = 0 -> Passed candidate.passed
    | "Killed" when candidate.failed > 0 && candidate.passed >= 0 -> Failed candidate.failed
    | "Invalid" when candidate.passed = 0 && candidate.failed = 0 -> Invalid "candidate did not compile"
    | _ -> Unavailable "candidate evidence is inconsistent"

let private advise execution candidate =
    let observation =
        { Baseline = Passed execution.baselinePassed
          Candidate = Some (evidence candidate)
          SnapshotMatches = execution.snapshotMatches
          RemainingBudget = 0
          Probe = None }

    let action, reason = observation |> Decision.decide |> Decision.describe
    {| id = candidate.id; action = action; reason = reason |}

[<EntryPoint>]
let main arguments =
    match arguments with
    | [| input |] ->
        try
            use stream = File.OpenRead input
            let options = JsonSerializerOptions(UnmappedMemberHandling = Serialization.JsonUnmappedMemberHandling.Disallow)
            let execution = JsonSerializer.Deserialize<Execution>(stream, options)
            if isNull (box execution) || isNull execution.candidates || Array.isEmpty execution.candidates
               || Array.exists (fun candidate -> isNull (box candidate) || String.IsNullOrWhiteSpace candidate.id) execution.candidates then
                eprintfn "Invalid decision evidence. Supply a nonempty list of identified candidates."
                65
            else
                let decisions = execution.candidates |> Array.map (advise execution)
                printfn "%s" (JsonSerializer.Serialize({| provider = "offline"; decisions = decisions |}))
                0
        with :? JsonException ->
            eprintfn "Invalid decision evidence JSON."
            65
    | _ ->
        eprintfn "Usage: DecisionAssist.Cli <execution.json>"
        64
