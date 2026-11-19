# Scrutiny, brainstorm and implemented scope

The useful asset in Canonflow.Samples is its combination of SQL constraints, F#
domain transitions, generated TypeScript validators, adversarial inputs and small
property suites. This gives agents examples spanning storage, domain and wire
boundaries. It also makes contradictions observable across those boundaries.

## Import decisions

| Input | Decision and reason |
|---|---|
| Canonflow.Samples `4b0fb817d70493f7cc0d4e3a454f92bd0ca187bd` | Retain 560 authored files, including all 24 F# projects; omit dependency/build payloads. The original root solution listed only two projects. |
| Foundation FSharpAssay.Playground `5e1b6f56e467496acdc572d5d0d0e2a1eae679ba` | Retain all 32 tracked files under `historical/legacy-playground`. Its old configuration and external project paths cannot drive the current tool. |
| Original generated reports and `PROOF.md` | Retain as historical artifacts. A “CERTIFIED” label and enum-generator tests do not establish behavioral equivalence. |
| Existing dogfood/Compose scripts | Retain without executing them. They assume sibling generators, fixed ports and external state. The new harness owns disposable containers. |
| Original Next.js frontend | Retain, outside the active qualification scope. Its dependencies and application behavior have not been qualified for deployment. |

`import-manifest.json` records original bytes and revisions. `migration-changes.json`
records current bytes and adaptations for all 592 imported files. No upstream
source checkout was changed by this import.

## Findings and repairs

| Finding | Implemented change and evidence |
|---|---|
| Banking restore required `/root/local-nuget` and an unused analyzer package | Remove the machine feed and unused reference; locked restore uses the official NuGet feed. The domain's local source attribute remains. |
| Airline syntax error; null airport input throws | Fix constructor syntax and null handling; invariant uppercase; acceptance regression. |
| Hospital `boolean` does not compile; existing-patient registration and existing-claim submission are unmatched | Use `bool` and explicit rejection for those states. Patient registration has an acceptance case; full claim workflow coverage remains a gap. |
| Wallet accepts negative and zero debits | Require a strictly positive debit; negative, zero and exact-balance regressions. |
| Webuy floating-point constructors accept NaN | Reject nonfinite coordinates, distance, radius, discount, surge, temperature and rating inputs. Explicit nonfinite acceptance probes cover coordinates, distance, radius and discount. |
| Geofence uses a constant mock distance | Haversine calculation with a clamped intermediate; near, far and same-location acceptance cases. This is spherical distance, not road routing. |
| Expiry constructs zero through a positive-only quantity and throws | Introduce nonnegative stock counts; quarantine availability at zero while preserving physical stock and reservations for explicit reconciliation. Order quantities stay positive. |
| Cancellation accepts a time before creation | Reject negative elapsed time; retain existing valid windows. |
| Any nonempty signature is considered valid | Fail closed with `SignatureVerifierNotConfigured`. No real cryptographic verifier is claimed. |
| NACK text is concatenated into JSON; parsed null is accepted | Serialize error data and reject null typed messages. |
| Identifier regex requires 16 characters but SQL field permits 15 | Align the domain and SQL with the supplied 15-character fixture. This is consistency validation, not tax-registration or checksum verification. |
| Webuy schema contains forbidden cross-table CHECK subqueries | Use write triggers with referenced-row locks. Guard referenced policy changes until an explicit reconciliation workflow exists. Probe valid writes, ten rejection cases and the inclusive rating boundary on real PostgreSQL. |
| Webuy distance property accepts unexpected successes | Require exactly the finite valid range; no NaN exemption or unconditional success branch. |
| Arangetram test project references a missing generator | Remove the missing compile item and unused namespace. The dummy test remains clearly classified as a placeholder. |
| Fintech API lacks its F# JSON converter dependency | Add its existing converter's explicit package reference; compile in the full solution. Its original external application workflow is not exercised. |
| Mock-drill client has a failing placeholder test command and repeated imports | Wire Jest, deduplicate imports, add TypeScript configuration, execute its six existing validator tests. No validator assertion was weakened. |
| Packaged FsAssay crashes on the valid alias `type Address = string` | Add an engine regression and safe symbol-name traversal that retains parent boundaries. Engine commit `3a70dc2` passes 121 tests. Admission stays unchanged. |

PostgreSQL documents why cross-table data cannot be checked this way in
[the PostgreSQL 18 constraint guidance](https://www.postgresql.org/docs/18/ddl-constraints.html).
The new triggers establish the tested write behavior. They do not establish a
general proof of all possible concurrent schedules or a complete data lifecycle.

## CSharpAssay.Playground crosswalk

| Capability | F# implementation |
|---|---|
| Broad sample inventory | All imported F# projects plus eleven added projects. All 35 active projects target .NET 11, with a separate inventory for loose files. |
| Functional design and pattern guidance | Private smart constructors, DUs, accumulated validation, function capabilities, audit as data, units of measure, recursive data, event folds and active patterns. See `samples/20-functional-patterns`. |
| Bounded effects and resilience | Bounded channel, fixed worker count, ordered outputs, linked cancellation and observed worker failure. Four async acceptance cases; final result storage still grows with output size. |
| Real PostgreSQL/HTTP workflow | New wallet sample with domain validation, parameterized SQL, atomic debits, replay intent checks, transactional outbox, loopback HTTP, authorization and bounded input. 22 executed acceptance cases. |
| Requirements change and mutation evidence | Frozen red acceptance results, retained legacy tests, isolated limit/amount/replay/wire mutations, disclosed survivor and invalid mutation. See `eng/mutations.py`. |
| Independent package consumer | Build the pinned FsAssay checkout, install its archive into isolated cache/tool paths, record archive SHA and producer revision. No engine ProjectReference. |
| Receipts and readable evidence | Canonical receipt, native assessment, SARIF, Markdown, HTML and per-required-suite TRX from the actual packaged tool. |
| Decision Assist and providers | Typed offline F# advice over actual wallet mutation results. Jev remains a separate opt-in adapter in the C# playground. |

## Remaining gaps

The generated F# and TypeScript suites include self-generated enum checks and
literal `true` placeholders. Passing them is useful execution evidence, with weak
semantic coverage. The new acceptance suites provide specific behavioral evidence.

Original generated output is retained without rerunning the CanonFlow generator.
SQL drift, migrations and hostile SQL remain input corpora, outside the initial
schema replay gate. Historical ColdProof examples intentionally contain unsafe
shapes and unresolved external references. The imported frontend is not part of
the build or deployment surface. None of these exclusions is reported as a pass.

The wallet host is an educational fixture: loopback HTTP, a configured bearer token,
single-request handling, synchronous database commands with timeouts, no TLS or
production identity service. Cancellation is checked before effects/commit; the
suite does not claim cancellation during blocked synchronous SQL. Outbox delivery,
process-crash recovery, reservation refunds and production availability remain open.
