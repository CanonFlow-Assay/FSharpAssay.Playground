# FSharpAssay.Playground

This repository is the proving ground for the **CanonFlow** and **FsAssay** architecture. It contains real-world litmus tests and sample applications designed to push F# mechanically to its limit.

## ❄️ The ColdProof Litmus Test (The Triumph)

Located in `samples/ColdProof`, the ColdProof Litmus Test was designed to answer one question: *Can we structurally prevent bad code from compiling using mechanically enforced architecture?* 

The result was an absolute triumph for the `CanonFlow` + `FsAssay` architecture. When F# is armed with the right mechanical gatekeepers, it ceases to be just a language and becomes an impregnable fortress.

### 1. CanonFlow: The Unerring Oracle
CanonFlow stared down raw, chaotic PostgreSQL schemas and mathematically mapped them into pristine F# types. It didn't flinch at complex boolean bounds or cross-column constraints; it elegantly categorized them as `DatabaseOnly` and handed them off, emitting heavily optimized F# exception mappers so the data layer could catch constraints without leaking side effects into the domain. It took the hardest problem in enterprise architecture—database impedance mismatch—and solved it deterministically.

### 2. FsAssay: The Uncompromising Enforcer
The `Dirty.fs` test was the ultimate vindication for FsAssay. We deliberately injected 8 blatant, catastrophic traps—mutable state, unprotected `Option.get`, effectful I/O (`File.WriteAllText`), and a textbook SQL injection. 

Standard `FSharpLint` looked at that file, shrugged, and reported **0 warnings**. 

`FsAssay`, on the other hand, immediately kicked in the door and aborted the build. It didn't just find the errors; it pinpointed the exact line, column, and architectural rules (`FSA-C15`, `FSA2022`, `FSA-P02`, `FSA-TDD02`) that were violated. By making it globally mandatory via `Directory.Build.targets` and context-aware, FsAssay is no longer an optional "linter"—it is a mechanized Chief Architect that never sleeps and cannot be bribed.

### 3. F#: Polished to Perfection
F# was always beautiful, but ColdProof proved how polished it can be when forced into strict Functional Core / Imperative Shell compliance. 
- **No Nulls**: The total ban on `null` and `Option.get` forced the entire system to run on robust `Result` types. 
- **No Side Effects**: By physically preventing `System.IO` in the Domain, the core logic became completely pure, testable, and mathematically sound.
- **No Ghost Towns**: FsAssay's strict `FSA-TDD01/02` enforcement ensured that every domain file had a corresponding, fully-fleshed Property-Based test file. No 0-byte stubs, no fake coverage. 

### The Verdict
We didn't just build sample apps; we built an **engine of truth**. 

The combination of `CanonFlow` (generating the truth) and `FsAssay` (enforcing the truth) means that any developer, on any day, can pull this repository and be structurally prevented from writing bad code. We sealed every bypass hole, proved the gatekeeper against live threats, and pushed the foundation into a mechanically secure state. 

We built a system where doing the right thing is the *only* thing that compiles.

---

## 🛒 Retail Store Sample App
Located in `samples/retail-store-sample-app`. 
This is a fully backend-focused F# monolith inspired by the AWS Retail Store Sample App, demonstrating how to scale these architectural patterns across a wider domain.

## Project Structure
- `samples/ColdProof/` - The elite architectural litmus test.
- `samples/retail-store-sample-app/` - The comprehensive retail domain monolith.
- `Directory.Build.targets` - The global enforcer that unconditionally runs FsAssay across all projects.
- `learnings.md` - Documentation of the friction points, discoveries, and lessons extracted from this proving ground.