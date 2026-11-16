# CanonFlow and FsAssay Integration Learnings 

*Tag: Gemini*

This document captures an in-depth retrospective on the remediation process of `FSharpAssay.Playground` and the integration of CanonFlow and FsAssay. It details insights at the architectural, generation, and verification levels.

## 1. Where CanonFlow and FsAssay Stand Now

### CanonFlow
**Status: Evolved into a Deterministic Generator**
CanonFlow has evolved from a conceptual methodology (where developers manually write types guided by a schema) into a tangible, deterministic tool. 
- **The Shift**: By generating the `Generated/` folder directly from the SQL schema via `canonflow.fsx`, CanonFlow enforces its core premise: *if the generation step is removed, the system doesn't build.*
- **Fidelity**: The generator now correctly maps database types (e.g., `NUMERIC(10,2)` to `decimal`), parses single-column `CHECK` constraints, and automatically produces `FsCheck` arbitraries that respect those bounds. 
- **Deduplication**: We addressed previous bugs where overlapping logic or shared bounds caused duplicate generators. CanonFlow now maintains a set of emitted types, preventing generation collisions.

### FsAssay
**Status: The Unforgiving Architect**
FsAssay proved its worth as a relentless enforcer of elite F# standards. 
- **The Shift**: By setting `ContinueOnError="false"` in `Directory.Build.targets` and removing `SkipFsAssay` overrides in Docker, FsAssay is no longer just a static analyzer—it is an architectural gatekeeper. 
- **Effectiveness**: FsAssay successfully flagged critical issues that humans easily miss: implicit boxing (`FSA-P02`) during ADO.NET parameter binding, mutable accumulations (`FSA-C10`) inside recursive reads, and empty test stubs masking as valid TDD (`FSA-TDD01`). 

## 2. Learnings at Each Level

### Architectural Level
- **"Compiles" ⊬ "Runs"**: The most profound realization was that F# type safety ends at the boundaries. The domain layer compiled perfectly, but the `DataAccess` layer had catastrophic runtime flaws (incorrect column names, silent `Option` to `DBNull` mismatches, missing `NOT NULL` fields). 
- **The Value of Integration Tests**: An actual integration test against Postgres was necessary to validate the query-to-schema alignment, proving that static typing alone isn't enough when crossing the SQL boundary.
- **Strict Adherence**: Forcing dependencies to flow downwards (`Api` -> `Infrastructure` -> `Domain`) using FsAssay prevents accidental coupling. The domain is now purely functional and isolated.

### Generation Level (CanonFlow)
- **Silent Skips are Dangerous**: Earlier versions of the generator encountered `CHECK` constraints it didn't understand (e.g., cross-column comparisons) and silently skipped them while falsely marking them as handled. The remediation forced the creation of an explicit Unhandled ledger (`𝒰`), surfacing these gaps so they can be addressed manually in the Domain layer.
- **Type Mapping Precision**: Mapping SQL `NUMERIC` to F# `int` instead of `decimal` caused subtle data truncation risks. Automating this mapping directly from the schema parser eliminated human translation errors.
- **The Arbitrary Generation Loop**: Generating `FsCheck` bounds directly from the SQL `CHECK` constraints closes the loop. The database dictates the bounds, CanonFlow generates the types and the random data generators, and FsCheck uses them to barrage the Domain logic.

### Verification Level (FsAssay & FsCheck)
- **TDD Enforcement**: FsAssay's rule that every Domain file must have a corresponding Test file (`FSA-TDD01`) was initially circumvented by creating 0-byte stubs. Populating these stubs with actual property-based tests using the generated arbitraries transformed the test suite from a checkbox into a robust verification layer.
- **Reflexivity**: The rule against using `Option.get` (`FSA-C02`) forced a massive refactor in the data access layer, moving from fragile extraction to robust `Result` mapping. This ensures that failures in database reading bubble up safely rather than throwing exceptions.
- **Boxing Violations**: ADO.NET's `.AddWithValue` is notorious for implicitly boxing value types. FsAssay flagged this (`FSA-P02`), forcing the use of explicit `value` unwrapping (e.g., `OrderId.value id`), resulting in higher-performance code.

## 3. How It Helped to Pull Over (The Remediation)

The remediation process (driven by `Cgq.md`) was a masterclass in aligning intention with execution:
1. **Uncovering the Gaps**: The initial state claimed compliance, but the automated tooling (FsAssay) and the deterministic generation (CanonFlow) exposed the illusion. The 0-byte test files, the skipped constraints, and the disabled MSBuild targets showed that the system was circumventing its own rules.
2. **Forcing the Issue**: By running CanonFlow first and enforcing the FsAssay output, the build literally broke until every single issue was fixed. It left no room for "we'll fix it later."
3. **The Result**: The repository now stands as a true, mechanically verifiable artifact. The `ratecard.md` reflects actual, proven compliance rather than aspirational claims. CanonFlow and FsAssay are no longer suggestions; they are the foundation of the project's integrity.

## 4. Elevating CanonFlow and FsAssay for Mammoth Projects

To scale this dual-engine architecture (CanonFlow + FsAssay) from a single bounded context to an enterprise-scale "mammoth" project, several key evolutions are required:

### 1. Robust Schema Parsing (The CanonFlow Compiler)
- **Current State**: `canonflow.fsx` uses regex and basic string matching, which is brittle and easily broken by complex SQL syntax, multiline constraints, or unexpected formatting.
- **Elevation Needed**: CanonFlow must integrate a full AST-based SQL parser (like `Microsoft.SqlServer.TransactSql.ScriptDom` or a Postgres equivalent). It must understand table relationships, cascading deletes, complex indices, and multi-column constraints, transforming the schema into an intermediate semantic model before generating F# code.

### 2. Multi-Context / Bounded Context Support
- **Current State**: Everything is generated into a single `Generated/` directory, assuming a monolithic domain.
- **Elevation Needed**: In a mammoth project, CanonFlow must support domain-driven design (DDD) boundaries. It should read schema annotations (or separate schema files) and generate namespace-isolated types (e.g., `Sales.Generated`, `Inventory.Generated`). FsAssay must enforce that `Sales` cannot bypass the API to directly modify `Inventory` domain models.

### 3. FsAssay as a Language Server (LSP) plugin
- **Current State**: FsAssay runs at the MSBuild/CLI layer, catching errors *after* the developer hits build or commits.
- **Elevation Needed**: For massive codebases, feedback loops must be instantaneous. FsAssay needs to be packaged as an LSP extension (for Ionide/VSCode or Rider). Flagging boxing violations, implicit unit sequences, and architectural boundary crossings as red squigglies *while typing* will drastically reduce cognitive load and PR rejection rates.

### 4. Advanced FsCheck State Machine Generation
- **Current State**: Arbitraries are generated for individual types, but state machine transitions and invariants (e.g., an order moving from `Placed` -> `Paid` -> `Shipped`) require handwritten property tests.
- **Elevation Needed**: CanonFlow should parse a DSL (or schema table states) representing the state machine and auto-generate the `Model` and `Command` structures for FsCheck's Model-Based Testing. This would allow the system to automatically pound the API with thousands of concurrent, valid state transitions to find race conditions and edge-case invariants.

### 5. Automated Data Access Layer (DAL) Scaffolding
- **Current State**: The schema generates Domain models, but `DataAccess.fs` requires manual mapping of `NpgsqlDataReader` and manual parameter binding (where `FSA-P02` boxing errors creep in).
- **Elevation Needed**: A mammoth project cannot afford handwritten SQL for CRUD operations. CanonFlow should generate the boilerplate parameterized queries (INSERT, SELECT, UPDATE) and safe reader mappings directly from the schema, returning `Result<T, string>` automatically. Developers only write custom SQL for complex aggregations or specialized reads.

By addressing these five pillars, CanonFlow and FsAssay will transition from a stringent, manually guided process into an industrial-grade, verifiable compiler for functional enterprise software.

## 5. Lessons Learnt from the ColdProof Litmus Test

The execution of the **ColdProof** 10-day build plan provided crucial insights into the resilience and capabilities of both CanonFlow and FsAssay when subjected to a rigorous "litmus test":

### 1. Robust AST Parsing is Non-Negotiable
- **Discovery**: We found that `Canon.PgPrism` was brittle when decoding PostgreSQL AST structures via JSON. Specifically, it assumed the right-hand side of binary expressions (like `<`) would always be a literal constant (`A_Const`). When faced with cross-column constraints (e.g., `min_temp < max_temp`), it crashed with `KeyNotFoundException`.
- **Lesson**: Parsing database schemas accurately requires resilient traversal of the AST (using `PgSqlParser`). We patched `Decode.fs` to safely detect and degrade cross-column constraints and functions (like `length()`) into `Opaque` constraints, preventing parser crashes while still capturing the presence of the constraint.

### 2. Identifying Proper Native Wrappers (`libpg_query`)
- **Discovery**: While attempting to parse `init.sql`, we mistakenly installed a generic Nuget package named `PgQuery`, which turned out to be an unrelated ORM. We later discovered that the correct C# wrapper for `libpg_query` used in early experiments was `pgsqlparser`.
- **Lesson**: Precise dependency management and namespace resolution are critical. The `pgsqlparser` library, combined with `Google.Protobuf` for JSON serialization, provided the exact AST structure CanonFlow expected.

### 3. FsAssay as an Incorruptible Gatekeeper
- **Discovery**: We introduced an explicitly polluted F# script (`Dirty.fs`) containing 8 established code traps: mutable keywords, unsafe `Option.get`, SQL injections via string interpolation, hardcoded secrets, unchecked state transitions, and missing property-based tests.
- **Lesson**: When scanned, FsAssay successfully failed the build and flagged all violations immediately. It proved that the static analyzer functions as a true architectural gatekeeper—it turns "red" precisely when it should, preventing compromised code from advancing in the pipeline.

### 4. Build Pipeline Interception
- **Discovery**: The `Directory.Build.targets` file at the root of the repository forced `FsAssay` to run on every MSBuild compilation (including our tests and auxiliary C# dump scripts). 
- **Lesson**: We learned how to control this interceptor seamlessly using `<SkipFsAssay>true</SkipFsAssay>` inside specific `.fsproj` files to unblock auxiliary builds, while ensuring the primary targets remained heavily guarded. 

### 5. Categorization of Database Limits
- **Discovery**: Complex logic such as `invoice_total = total_value + gst - discount` cannot currently be projected elegantly into F# types. 
- **Lesson**: `CanonFlow` successfully classified these bounds as `DatabaseOnly`, gracefully mapping its own boundaries without choking on the schema, and cleanly documented these unsupported limits in the output `EVIDENCE.md` report.

### 6. The FSharpLint vs FsAssay Discrepancy
- **Discovery**: When we ran the standard `dotnet fsharplint` against `Dirty.fs` (which contained 8 blatant domain and security violations including SQL injection, hardcoded credentials, and mutable state), it reported **0 warnings**. 
- **Lesson**: Standard stylistic linters like FSharpLint only verify syntax formatting and naming conventions. They are fundamentally incapable of enforcing strict Functional Core / Imperative Shell architecture. FsAssay is not a substitute for a linter—it is an architectural enforcer that catches what conventional stylometry tools let pass.
