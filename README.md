# FSharpAssay.Playground

A practical F# corpus for agents to inspect, change and verify: enterprise domain
models lifted from [Canonflow.Samples](https://github.com/ArunNotFound/Canonflow.Samples),
functional patterns, bounded concurrency, and a real PostgreSQL/HTTP wallet.
It consumes an independently packaged, pinned
[FsAssay](https://github.com/CanonFlow-Assay/FSharpAssay) tool.

The import retains **560 authored sample files and all 24 original F# projects**.
Eight added projects bring the checked solution to **32 projects**. The previous
Foundation playground's 32 files remain under `historical/legacy-playground`.
Dependency and build payloads were omitted; both upstream checkouts were preserved.

## Start here

| Location | What an agent can learn or exercise |
|---|---|
| `samples/10-enterprise` | Airline, banking, hospital, trading, gatepass, clubs, messaging, commerce and wallet domains; SQL constraints, transitions, generated validators and adversarial inputs. |
| `samples/20-functional-patterns` | Smart constructors, DUs, validation accumulation, function capabilities, audit data, units of measure, recursive data, event folds, active patterns and bounded task orchestration. |
| `samples/40-wallet` | Typed debit validation, parameterized PostgreSQL transactions, concurrent balance protection, replay intent checks, transactional outbox and bounded loopback HTTP. |
| `tests` | 31 enterprise regressions, 14 pattern cases and 22 real wallet cases. All 14 imported .NET suites remain runnable. |
| `eng` | Reproducible import checks, isolated package consumption, disposable database qualification, Jest execution and six isolated mutation probes. |
| `docs/evidence` | Actual red TRX, initial build/schema failures, the FsAssay alias ToolFailure, and disclosed mutation outcomes. |

Read [the scrutiny and implemented brainstorm](docs/scrutiny.md) for findings,
repairs, a CSharpAssay.Playground crosswalk and remaining gaps. Read
[qualification instructions](docs/qualification.md) for exact prerequisites,
commands, evidence scope and CI behavior.

## Run

Use exact .NET SDK **10.0.301**, Python 3.12+, Node 26/npm and a running Docker daemon.
The engine checkout must match `workflows/assay.json`.

```sh
python3 eng/provenance.py
python3 eng/prepare.py --engine ../FSharpAssay
python3 eng/qualify.py --output artifacts/qualification
python3 eng/clients.py --output artifacts/clients
python3 eng/mutations.py --output artifacts/mutations
```

Use a clean committed checkout for authority. While editing, `eng/qualify.py
--development --output artifacts/development` runs the application checks without
the clean-candidate authority step. Each output directory must be new.

The database harness creates and removes only its own disposable PostgreSQL 18.7
container, using an ephemeral password and random loopback port. It requires no
existing service or global FsAssay installation. The wallet API is an educational
fixture, not a deployment template.

## What the evidence means

The new acceptance cases test specific boundaries and effects. Imported suites
also contain dummy properties and generator checks; their passing counts do not
establish complete behavior. Generated reports and old “CERTIFIED” labels remain
historical material. The original frontend, generator execution, drift/migration
SQL and historical ColdProof project are outside active qualification.

FsAssay currently admits **zero blocking and zero advisory rules**. `verify`
executes the registered acceptance suites and emits a four-state receipt, native
assessment, SARIF, Markdown and HTML. `check` leaves required tests unexecuted and
must remain Inconclusive. Experimental findings are observations, not a purity,
security or business certificate.

Exact original revisions and file hashes are in [the import manifest](docs/import-manifest.json).
Reviewed adaptations are in [the migration ledger](docs/migration-changes.json).
See [the retrospective](docs/retrospective.md) for the lessons and follow-up limits.

Apache-2.0; source attribution is retained in `NOTICE`.
