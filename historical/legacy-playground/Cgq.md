# FSharpAssay.Playground Remediation — A Formal Discharge Specification

**Target** `CanonFlowFoundation/FSharpAssay.Playground` · **Baseline** `8597a5b` · **Mode** law-verified, gate-driven.
**Synthesis** of three independent scrutinies (Claude, Qwen, GPT); every claim re-verified against `8597a5b`.

The project's own thesis is `delete Generated/ ⟹ ⊥`. It holds trivially (any `.fsproj`-included file breaks the build) and proves too little. The real lessons: **generated types are not proof of fidelity, a scanner's output is not enforcement, and "compiles" is not "runs."** Laws `L₁…L₁₂`, witnessed violations `¬Lᵢ` (with loci), obligations `Oᵢ ⊢ Lᵢ` discharged only by mechanical witness. Companion to the `FSharpAssay` spec; same grammar.

---

## 0. Objects

```
𝒮            schema (db/init/01-schema.sql);  𝒦 its constraints;  cols(𝒮) its columns
col : Column → SqlType                NUMERIC(10,2), INT, VARCHAR, UUID, TIMESTAMPTZ
nn ⊆ cols(𝒮)                          NOT-NULL columns without DEFAULT
𝒢 = canonflow.fsx                     generator;  G(𝒮) = {Types, Validators, Generators, PROOF}
τ : Constraint → FSharpType ∪ {⊥}     generated base type (⊥ = skipped)
verdict : Constraint → {Proven, PartiallyProven, ManualObligation, Unsupported}
enforce : Constraint → Loc ∪ {none};  𝒰 ⊆ 𝒦 the Unhandled ledger
fsharp : SqlType → FSharpType          NUMERIC↦decimal, INT↦int, VARCHAR/TEXT↦string, UUID↦Guid, TIMESTAMPTZ↦DateTimeOffset
𝒬            the set of SQL statements in the data-access layer; cols(q) columns referenced by q
```

**Constraint arity.** `col-level` (one column) vs `row-level` (arithmetic / cross-column / temporal). The generator discharges only `Column → SmartConstructor`; row-level needs `Row → Invariant`; entity/FK generation is absent.

---

## 1. Census and gaps  *(authoritative, `comm`- and schema-verified)*

`|𝒦_chk| = 40` (39 unique + 1 duplicate `chk_shipped_before_delivered`).

```
emitted single-column smart constructors : 29   (of which 4 wrong base type, 2 with a conjunct dropped)
silently skipped (enforce = none)         : 10   (all falsely proofed "Handled via Record/Option")
entities / FK relationships generated      : 0   (records handwritten in Models.fs; 7 IDs hardcoded by name in 𝒢)
```

$$\text{col-level faithful} \approx \tfrac{23}{40}=58\%,\quad \text{silently dropped} = \tfrac{10}{40}=25\%,\quad \text{data-access executable against } 𝒮 : \text{false}$$

---

## 2. Laws

> **L₁ — No silent skip.** `∀k∈𝒦. enforce(k) ≠ none ∨ k ∈ 𝒰`. The `skip` path appends to `𝒰`; nothing is dropped invisibly.

> **L₂ — Type fidelity.** `τ(k) ≠ ⊥ ⟹ base(τ) = fsharp(col(c))` — from the **column** type, never the CHECK surface. `NUMERIC↦decimal`; `item_count:int` correct, `subtotal:int` not.

> **L₃ — Illegal states unrepresentable (row + aggregate).** every row-level `k` is enforced by a `Row → Result<unit,_>` validator invoked from the entity smart constructor, or `k ∈ 𝒰`. Aggregate constructors **compute** derived fields (`ItemCount = |Items|`, `Subtotal = Σ line`, `Order.total = subtotal+tax+shipping−discount`) so contradictory aggregates cannot be built.

> **L₄ — Four-valued proof honesty.** `verdict(k) ∈ {Proven, PartiallyProven, ManualObligation, Unsupported}`; `PROOF.md` prints the verdict with a falsifiable `Enforced-At` locus. Only `Proven` ⟹ ✅. Under **strict** generation, any non-`Proven` without an auditable manual proof **fails the build**. No constraint is printed handled unless `enforce(k) ≠ none`.

> **L₅ — Regeneration determinism (the real North Star).** from a clean checkout containing only `𝒮`, running `𝒢` reproduces every `Generated/` artifact byte-identically (`git diff --exit-code`); the build **runs `𝒢` before compile**; strict mode halts on any `Unsupported`. "Delete a committed file → build breaks" is necessary but insufficient; the app must depend on the *process*, not just its committed output.

> **L₆ — Verification layer generated + wired.** FsCheck arbitraries generated from bounds; `∀` generated `T`, `∃` property `create(Arb_T) ⟺ constraint(T)`, driven by the generated `Arb`; every test file in the project is non-empty and compiled (no phantom stubs).

> **L₇ — Evidence closure + portability.** `codes(ratecard) ⊆ codes(SARIF) ⊆ 𝒞`; every SARIF finding fixed / suppressed-with-reason / acknowledged; no ghost codes; SARIF URIs are **repo-relative** (no absolute `file://E:/…`, no leaked local paths); no contradiction (ratecard "0 mutations" ⟹ `FSA-C10 ∉ SARIF`).

> **L₈ — Gate armed (all layers agree).** the gate is enabled at every layer or the claim is struck: MSBuild (`ContinueOnError="false"`), Docker (no `SkipFsAssay`), CI (a workflow exists), a **resolvable** FsAssay path (not a sibling-repo relative path), and a `.fsassayrc`/profile that FsAssay actually consumes. Disabled in any layer ⟹ disabled.

> **L₉ — Reference-app reflexivity.** `Option.get` on a smart-constructor result is forbidden in `src/`; DB decoders return `Result`; no `mutable` accumulation. No `FSA-C02`/`FSA-C10` in `Domain/`/`Infrastructure/` except as documented, justified exceptions.

> **L₁₀ — Doc coherence.** `todo.md §4` structure, schema counts, and Onion direction (`Api → Infrastructure → Domain`) are consistent across docs and match the tree; no duplicate constraints in `𝒮`; defenses of findings (e.g. "TDD01 is consolidated testing") are true, not overclaims.

> **L₁₁ — Enumerations are DUs.** an `IN (v₁,…,vₙ)` constraint generates `type T = C₁ | … | Cₙ` with `ofString`/`toString`, not `private T of string`. Consequence: totality at use sites — no `T.create "literal" |> Option.get`.

> **L₁₂ — Executable query/schema agreement.** `∀ q∈𝒬. cols(q) ⊆ cols(𝒮)`; every INSERT supplies `target(q) ∩ nn`; read casts match `col` (`GetDecimal` for `NUMERIC`, not `GetInt32`); the runtime connection config is read from the deployment environment and matches it. "Compiles" ⊬ "runs"; only an integration test against `𝒮` witnesses executability.

---

## 3. Witnessed violations at `8597a5b`

| # | Violates | Witness (verified) |
|---|----------|--------------------|
| V1 | L₁,L₄ | **10 constraints silently skipped, all falsely proofed.** `mapConstraint` `skip` on `= *`/`IS NULL OR`/`else` (`canonflow.fsx:55–60`); `PROOF.md` marks all 10 `⚠️ Handled via Record/Option` (`:102`). See `𝒰`. |
| V2 | L₂ | **Money `int` over `NUMERIC(10,2)`.** `CartSubtotal, OrderTax, OrderShipping, OrderDiscount : int` (`Types.fs:74,130,138,146`) vs cols `NUMERIC(10,2)` (`schema:53,88,89,90`); inference keys on `.` in the CHECK string (`canonflow.fsx:39`). |
| V3 | L₃ | **Row + aggregate invariants representable.** `chk_order_total`, `= unit·qty` totals, temporal/state rules dropped; `OrderDiscount` drops `≤ subtotal`; `Cart` record admits `ItemCount ≠ |Items|`, `Subtotal ≠ Σ`. |
| V4 | L₅ | **Generation not deterministic-in-pipeline.** `Generators.fs`/`Validators.fs` empty stubs; committed output is authoritative; local build does **not** run `𝒢`; no strict/fail-closed mode; entity+FK generation absent (7 IDs hardcoded, `canonflow.fsx:104`). |
| V5 | L₆ | **Verification layer hollow.** only `Properties.fs` (4 properties) is compiled; the 7 mirror test files are **0 bytes** and excluded (`Domain.Tests.fsproj` includes `Properties.fs` only). |
| V6 | L₇ | **ratecard ⊄ SARIF + non-portable.** ratecard cites `FSA1001/1004` (∉ SARIF), claims "0 mutations" while SARIF has `FSA-C10 ×1`, omits `FSA-C02 ×18`/`FSA-F04 ×39`/`FSA-AI10 ×11`; SARIF carries 118 absolute `file://E:/github/…` URIs + a malformed `file://Architecture`. |
| V7 | L₈ | **Gate disabled at five layers.** `Directory.Build.targets ContinueOnError="true"` (`:6`); Dockerfile `-p:SkipFsAssay=true` on build+publish; no `.github/workflows/`; FsAssay invoked via sibling path `../FSharpAssay/…` (`:4`); `.fsassayrc` keys + `profile:production` not in FsAssay's config model → Core fallback. |
| V8 | L₉,L₁₁ | **Showcase violates its creed — root cause L₁₁.** 18 `FSA-C02` real: 7 in `StateMachines.fs` (`XStatus.create "…" \|> Result.toOption \|> Option.get`), ≥3 in `DataAccess.fs` decoders; `FSA-C10 ×1` = `let mutable items` accumulation (`DataAccess.fs:56`). |
| V9 | L₁₀ | **Plan/doc incoherence + schema dup.** §4 12/6/8 files → tree 2/1/1 (consolidated, not absent); counts 12·47·8 → **9·40·11**; ratecard vs Findings assert opposite "Onion" directions; `chk_shipped_before_delivered` duplicated; "TDD01 = consolidated testing" defense refuted by the 7 empty stubs. |
| V10 | L₁₂ | **Data-access cannot run against its own schema.** See table below. |

**`𝒰` — authoritative skip ledger (all falsely proofed):** `chk_order_total`, `chk_order_item_total`, `chk_cart_total_price` (arithmetic); `chk_compare_price`, `chk_order_discount ≤ subtotal` (cross-col); `chk_confirmed_before_shipped`, `chk_shipped_before_delivered`, `chk_cancelled_no_delivery`, `chk_delivered_no_cancel`, `chk_auth_before_capture` (temporal); `chk_completed_has_order` (state+nullable).

**V10 — executable query/schema mismatches (`DataAccess.fs`):**

| SQL site | mismatch | schema truth | runtime failure |
|---|---|---|---|
| `getProduct` SELECT (`:14`) | `stock_quantity` | col is `stock` (`schema:32`) | column does not exist |
| `getCart`/`saveCart` (`:48,93`) | `GetInt32(subtotal)` / `CartSubtotal:int` | `subtotal NUMERIC(10,2)` | invalid cast / truncation |
| `saveCart` (`:79,91`) | `customer_email = DBNull` when `None` | `customer_email … NOT NULL` (`:50`) | NOT NULL violation |
| `saveCart` cart_items (`:103`) | omits `total_price` | `NOT NULL` (`:69`) | NOT NULL violation |
| `saveOrder` orders (`:122`) | col `shipping` | col is `shipping_cost` (`:89`) | column does not exist |
| `saveOrder` orders (`:122`) | omits `total, shipping_address, shipping_city, shipping_state` | all `NOT NULL` (`:91–94`) | NOT NULL violation |
| `saveOrder` orders (`:132`) | `shipping_pincode = DBNull` when `None` | `NOT NULL` (`:95`) | NOT NULL violation |
| `saveOrder` order_items (`:137`) | omits `sku, name, total_price` | all `NOT NULL` (`:123,124,127`) | NOT NULL violation |
| `connString` (`:8`) | `Database=retail;Username=postgres;Password=postgres` hardcoded; `CONNECTION_STRING` never read | compose: db `retailstore`, user `retail`, injects `CONNECTION_STRING` | connect/auth failure |

---

## 4. Proof obligations

Order `≺`: `O₁ ≺ {O₃,O₄}`; `O₂ ≺ {O₆,O₁₂}`; `O₁₁ ≺ O₉`; `O₁₂ ≺ Φ⋆`.

**O₁ ⊢ L₁** — loud skip. `skip → Unhandled reason`; append to `𝒰`; emit `UNHANDLED` PROOF row. Discharge: `chk_`-count = PROOF-rows; `! grep -q "Handled via Record/Option" src/Generated/PROOF.md`.

**O₂ ⊢ L₂** — type from column DDL. Parse column types; `base(τ)=fsharp(col)`. Discharge: `! grep -nE "type (Cart|Order)(Subtotal|Tax|Shipping|Discount).* of int" src/Generated/Types.fs`; property `equal SqlType ⟹ equal base`.

**O₃ ⊢ L₃** — row + aggregate constructors. emit `Generated.RowValidators` (`Row → Result<unit,_>`) for arithmetic/temporal/cross-col; entity constructors compute `ItemCount/Subtotal/total` and reject mismatches. Discharge: property `Order.create (total ≠ Σ) = Error`; `Cart.create` with `ItemCount ≠ |Items|` = `Error`.

**O₄ ⊢ L₄** — four-valued PROOF. `verdict` column + `Enforced-At`; strict mode fails on non-`Proven` lacking a manual proof. Discharge: `∀ ✅/Proven`, locus exists and guards; strict run exits nonzero while `𝒰 ≠ ∅` without manual proofs.

**O₅ ⊢ L₅** — regeneration determinism. build runs `𝒢` before compile; `dotnet fsi canonflow.fsx && git diff --exit-code src/Generated` clean; strict mode halts on `Unsupported`. Discharge: the two commands, green.

**O₆ ⊢ L₆** — generate + wire tests. generate arbitraries (dep O₂); populate or delete the 7 stub files; every `<Compile>`'d test file non-empty. Discharge: `! find tests -name '*.fs' -empty`; `#[<Property>] ≥ |generated types|`.

**O₇ ⊢ L₇** — evidence closure + portable SARIF. regenerate `ratecard.md` from SARIF; repo-relative URIs. Discharge: `comm -23 codes(ratecard) codes(SARIF)` empty; `! grep -q "file://E:/\|file://Architecture" output/fsassay-scan.sarif`.

**O₈ ⊢ L₈** — arm the gate everywhere. `ContinueOnError="false"`; remove `SkipFsAssay`; add CI (build→canonflow→assay→test); resolve FsAssay via tool/submodule not sibling path; align `.fsassayrc`/profiles to FsAssay's model or strike the claim. Discharge: `! grep -q 'ContinueOnError="true"' Directory.Build.targets`; `! grep -rq SkipFsAssay Dockerfile`; `.github/workflows/` exists; a critical specimen makes the build exit nonzero.

**O₉ ⊢ L₉** — reference app obeys creed (dep O₁₁). remove `Option.get` on constructor results; decoders return `Result` (bind/applicative); no `mutable`. Discharge: `! grep -rn "Result.toOption |> Option.get\|let mutable" src/`; scan 0 `FSA-C02`/`FSA-C10` in `Domain/`+`Infrastructure/`.

**O₁₀ ⊢ L₁₀** — reconcile docs; delete the duplicate constraint; correct/remove the TDD01 defense. Discharge: doc-lint counts = schema `grep`; single correct Onion statement.

**O₁₁ ⊢ L₁₁** — enumerations as DUs. `IN` → closed DU + `ofString`/`toString`; `Models.fs`/`StateMachines.fs` use DU values. Discharge: `! grep -nE "type (Order|Cart|Checkout|Payment|Shipment)(Status|Method) = private .* of string" src/Generated/Types.fs`.

**O₁₂ ⊢ L₁₂** — executable query/schema agreement (dep O₂). fix every mismatch in the V10 table; supply all `nn` columns; `GetDecimal` for `NUMERIC`; read `CONNECTION_STRING` from env. Add a **real PostgreSQL integration test** (Testcontainers or the compose DB) that round-trips product→cart→order. Discharge: a query/schema lint (`cols(q) ⊆ cols(𝒮)`, `nn ⊆ written(q)`) passes; the integration test is green.

---

## 5. Fidelity slice `Φ⋆` — one genuinely executable aggregate

Freeze to **`Order`** (richest failure set). Close, end-to-end: `subtotal/tax/shipping/discount` as `decimal` (O₂); `OrderStatus` a DU (O₁₁); `chk_order_total`, `discount ≤ subtotal`, and the confirmed→shipped→delivered chain as row-level validators in a checked `Order.create` that computes `total` (O₃); honest four-valued `PROOF.md` rows (O₄); the orders/order_items SQL repaired and all `nn` columns supplied (O₁₂); env-driven connection string; a green Postgres round-trip integration test; zero `Option.get` in the Order path (O₉). No aggregate scales past `Φ⋆` until `Order` closes `O₁–O₆,O₉,O₁₁,O₁₂`.

---

## 6. GATE-FINAL

$$\Omega \;\triangleq\; \bigwedge_{i=1}^{12} L_i \;\wedge\; \Phi^\star$$

`Ω` holds iff every `Oᵢ` discharge and `Φ⋆` are green in one run; `PROOF.md`/`ratecard.md` regenerated from `𝒮`/SARIF; the gate blocks a critical specimen; and the North Star is the **strong** one: `dotnet fsi canonflow.fsx && git diff --exit-code` clean, and strict mode halts on `Unsupported`.

**Ledger.** One line per obligation → `REMEDIATION_LEDGER.md`: `Oᵢ | Lᵢ | commit | discharge-cmd | result | |𝒰| | ts`.

---

### Non-findings (verified false — do not action)
Recorded so no executor chases them: (a) *"README claims 14 tests / 19 endpoints / 4 state machines"* — the README contains none of these strings. (b) *"No state-machine / API / Infrastructure code found"* — `StateMachines.fs`/`Handlers.fs`/`DataAccess.fs` exist with real content; consolidated, not absent. Their real defects are `L₉`/`L₁₁`/`L₁₂`.

### What holds (credit — do not regress)
The stress case is well-chosen; private smart-constructor scalar boundaries are real; SQL is parameterized and cart writes are transactional; compile ordering makes `Types.fs` structurally load-bearing; `PROOF.md` inventories all 40 constraints (verdicts need correction). Reframe: **an executable conformance laboratory, not yet a reference application.** The deep lesson the runtime defects prove: the V10 mismatches are the *predicted* consequence of leaving the data-access and entity layers **un-generated** — hand-written glue drifts from `𝒮`. That is the empirical case for CanonFlow's own thesis: generate the boundary, don't transcribe it. This spec forbids the three overclaims (`money-as-int`, `Handled via Record/Option`, `illegal states cannot be represented`) — and now a fourth (`it compiles, therefore it works`) — from being asserted until true.
