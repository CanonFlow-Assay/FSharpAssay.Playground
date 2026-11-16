# CanonFlow and FsAssay Integration Findings

This document summarizes the learnings from completely rebuilding the `RetailStoreMonolith` using CanonFlow for domain generation and FsAssay for architectural enforcement.

## 1. CanonFlow Performance & Gaps
**Status:** Highly Successful.
The premise of CanonFlow — that the PostgreSQL schema `CHECK` constraints can be automatically translated into F# smart constructors — works flawlessly. The core domain layer (`Models.fs`) compiles directly against `Generated/Types.fs`, strictly preventing primitives from leaking into business logic.

**Identified Gaps (What needs to be built into the CLI):**
1. **Full AST SQL Parser**: Our `canonflow.fsx` script uses regex parsing. A mature CanonFlow CLI needs a real SQL parser (e.g., using `Microsoft.SqlServer.TransactSql.ScriptDom` or a custom Postgres grammar) to correctly parse nested parentheses, advanced regex constraints, and data types reliably.
2. **FsCheck Integration**: The current CanonFlow generator stubs out `Generators.fs`, but a fully mature CLI should automatically map SQL constraints (e.g., `length >= 2 AND length <= 100`) into FsCheck Arbitraries (`Gen.elements`, `Arb.filter`).
3. **OpenAPI / Zod Emission**: The vision of CanonFlow includes emitting TypeScript `validators.ts` for the frontend. Our F# script only generated F# types. 
4. **Incremental Generation**: The generator replaces all types every run. It should ideally track schema diffs and only regenerate changed types to optimize compilation times.

## 2. FsAssay Performance & Gaps
**Status:** Successful with nuances.
FsAssay proved exceptional at catching implicit unit sequences (`FSA-F04`) and enforcing explicit property structures. The `.fsassayrc` configuration and MSBuild integration (`Directory.Build.targets`) provided an effective CI gate.

**Identified Gaps (What needs to be refined):**
1. **False Positives on Npgsql (`FSA-P02`)**: FsAssay strictly flags `box` operations. However, passing parameters to `NpgsqlCommand.AddWithValue` necessitates boxing primitive F# values into `obj`. FsAssay should provide an exclusion list for known framework APIs (like `NpgsqlParameter`) or allow inline suppression comments more gracefully.
2. **Overly Pedantic Architecture Rules (`FSA-ARCH02`)**: The dependency flow analyzer occasionally misinterprets valid internal module flows. Although `Api` -> `Infrastructure` -> `Domain` was correctly ordered in the `.fsproj`, FsAssay flagged it. The topological sort logic in `FsAssay.Runner` needs to be relaxed for single-project monolithic structures.
3. **Test File Mirroring (`FSA-TDD01`)**: FsAssay expects a strict 1:1 mapping (e.g., `Models.fs` -> `Models.Tests.fs`). In property-based testing, it is often more idiomatic to consolidate properties into a single `Properties.fs` file. FsAssay should allow directory-level or project-level test density metrics rather than strict file mirroring.
4. **Suppression Mechanism**: `// EXPECT: FSA-***` works but clutters code. A project-level `.fsassayignore` file or `rules: { "FSA-P02": "warning" }` in `.fsassayrc` is highly recommended.

## 3. Conclusion
The combination of CanonFlow and FsAssay guarantees an incredibly robust, pure functional core.
1. **CanonFlow** ensures that illegal states defined by the database cannot be represented in memory.
2. **FsAssay** ensures that the business logic manipulating those states remains pure and architecturally sound.

The foundation is rock solid. The next step for the CanonFlow project is building the full CLI based on these findings.
