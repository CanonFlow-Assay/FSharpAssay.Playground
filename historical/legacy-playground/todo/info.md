organize all retai store files to new folder 


create a new folder for below and do use docker to do 

workflow -- CanonFlow -> FSharpassay  -> FSlint -renforce goodness into loop 

capture learnings

some analogy  this  CERN expreimant  

air tioght experiment , 
like caged phoneix 
with golden handcuff do 


# ColdProof — The Litmus Test App

## A cold-chain pharmaceutical tracker designed to make CanonFlow and FsAssay fail loudly.

A litmus test isn't one that passes. It's one that **turns color when it should.** So ColdProof isn't built to showcase the tools — it's built with **14 embedded traps** that a buggy CanonFlow or FsAssay will silently pass and a correct one will catch. If the tools pass ColdProof, they're real. If they pass it *without* catching the traps, they're lying.

---

## Why This App

Cold-chain pharma distribution is the perfect stress specimen because a single mistranslation has a **visible, serious consequence** — not a theoretical one. A validator that's stricter than PostgreSQL rejects a batch already sitting in the warehouse. A validator that's looser ships spoiled insulin. There's no "close enough."

It also happens to contain **every hard constraint class** CanonFlow claims to handle, and **every temptation** FsAssay claims to resist:

| CanonFlow stress | Where it lives |
|---|---|
| Regex `~` (newline anchoring) | `batch_no`, `license_no` |
| `length()` (runes vs UTF-16) | `inspector_notes` |
| Multi-column arithmetic | `invoice_total = total_value + gst - discount` |
| Temporal ordering | `shipped_at IS NULL OR released_at IS NOT NULL` |
| Nullable-in-CHECK (**three-valued logic**) | `inspector_notes IS NULL OR length(...) <= 2000` |
| ENUM → DU | `storage_class` |
| UNIQUE / FK (**DatabaseOnly**) | `batch_no`, `batch_id` |
| JSONB (**lossy**) | `metadata` |
| `NUMERIC(p,s)` money (**ExactWithin**) | all money columns |
| Time-dependent constraint | `expires_at > CURRENT_DATE` |

| FsAssay stress | The temptation |
|---|---|
| FSA1001 (mutable) | spoilage counter accumulation |
| FSA1002 (Option.get) | `released_at` |
| FSA1006 (failwith) | "batch expired" |
| FSA2022 (I/O in domain) | reading temperature logs |
| FSA-SEC01 (secrets) | DB connection string |
| FSA-SEC02 (SQL injection) | batch lookup by id |
| State machine | QUARANTINED → SHIPPED skipping RELEASED |
| FSA-AI01 (dead code) | AI-generated helper nobody calls |

---

## The Schema (Traps Annotated)

```sql
CREATE TYPE storage_class AS ENUM ('FROZEN', 'COLD', 'COOL', 'AMBIENT');

CREATE TABLE batches (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_no      VARCHAR(20)   NOT NULL,
    drug_name     TEXT          NOT NULL,
    storage_class storage_class NOT NULL,
    min_temp      NUMERIC(4,1)  NOT NULL,
    max_temp      NUMERIC(4,1)  NOT NULL,
    received_at   TIMESTAMPTZ   NOT NULL DEFAULT now(),
    expires_at    DATE          NOT NULL,
    units         INT           NOT NULL,
    license_no    VARCHAR(20)   NOT NULL,

    -- TRAP 1: regex. Naive .NET "^…$" admits "AB-123456\n". PG rejects.
    CONSTRAINT chk_batch_no  CHECK (batch_no ~ '^[A-Z]{2,4}-[0-9]{6}$'),
    -- TRAP 2: cross-column comparison. Not a smart constructor. Row validator.
    CONSTRAINT chk_temp_range CHECK (min_temp < max_temp),
    CONSTRAINT chk_units      CHECK (units > 0 AND units <= 100000),
    CONSTRAINT chk_license    CHECK (license_no ~ '^[A-Z]{2}-[0-9]{2}-[A-Z]{1,3}-[0-9]{4}$'),
    -- TRAP 3: time-dependent. Cannot be a pure constructor — needs injected clock.
    CONSTRAINT chk_not_expired CHECK (expires_at > CURRENT_DATE),
    -- TRAP 4: UNIQUE. DatabaseOnly, NOT Unsupported. CanonFlow must classify, not translate.
    UNIQUE (batch_no)
);

CREATE TABLE temperature_logs (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_id    UUID NOT NULL REFERENCES batches(id),   -- TRAP 5: FK → DatabaseOnly
    reading     NUMERIC(4,1) NOT NULL,
    sensor_id   TEXT,
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_reading CHECK (reading >= -80 AND reading <= 60)
);

CREATE TABLE shipments (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    batch_id        UUID NOT NULL REFERENCES batches(id),
    status          VARCHAR(20) NOT NULL DEFAULT 'QUARANTINED',
    quantity        INT NOT NULL,
    destination     TEXT NOT NULL,
    total_value     NUMERIC(12,2) NOT NULL,
    gst             NUMERIC(12,2) NOT NULL DEFAULT 0,
    discount        NUMERIC(12,2) NOT NULL DEFAULT 0,
    invoice_total   NUMERIC(12,2) NOT NULL,
    spoilage_units  INT NOT NULL DEFAULT 0,
    inspector_notes TEXT,
    metadata        JSONB,                              -- TRAP 6: JSONB → lossy, declared
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    released_at     TIMESTAMPTZ,
    shipped_at      TIMESTAMPTZ,
    delivered_at    TIMESTAMPTZ,

    CONSTRAINT chk_status CHECK (status IN ('QUARANTINED','RELEASED','SHIPPED','DELIVERED','SPOILED')),
    CONSTRAINT chk_quantity CHECK (quantity > 0),
    -- TRAP 7: spoilage bounded by quantity (cross-column).
    CONSTRAINT chk_spoilage CHECK (spoilage_units >= 0 AND spoilage_units <= quantity),
    -- TRAP 8: multi-column arithmetic. The classic row validator.
    CONSTRAINT chk_invoice CHECK (invoice_total = total_value + gst - discount),
    -- TRAP 9 & 10: temporal ordering.
    CONSTRAINT chk_released_before_shipped CHECK (shipped_at IS NULL OR released_at IS NOT NULL),
    CONSTRAINT chk_shipped_before_delivered CHECK (delivered_at IS NULL OR shipped_at IS NOT NULL),
    -- TRAP 11: state × temporal.
    CONSTRAINT chk_spoiled_not_delivered CHECK (status != 'SPOILED' OR delivered_at IS NULL),
    -- TRAP 12: NULLABLE-IN-CHECK. The three-valued logic bomb.
    --   PG: NULL notes → UNKNOWN OR … → admitted.
    --   Naive F#: notes.Length <= 2000 on a None → crash or reject.
    CONSTRAINT chk_notes CHECK (inspector_notes IS NULL OR length(inspector_notes) <= 2000),
    -- TRAP 13: length() counts runes in PG, UTF-16 units in .NET.
    --   "🧪"×1001 is 1001 chars in PG (reject), 2002 in .NET (also reject) —
    --   but "🧪"×1500 is 1500 in PG (accept) and 3000 in .NET (reject). Divergence.
    CONSTRAINT chk_discount CHECK (discount >= 0 AND discount <= total_value)
);
```

**13 traps in ~70 lines of DDL.** Every one is a real constraint a pharma schema would actually have. None are synthetic.

---

## The State Machine (FsAssay's Specimen)

```
QUARANTINED ──release──→ RELEASED ──ship──→ SHIPPED ──deliver──→ DELIVERED
     │                       │
     └──────spoil────────────┴──spoil──→ SPOILED
```

Three properties make this a real test:

1. **Irreversibility.** RELEASED cannot return to QUARANTINED. SHIPPED cannot become SPOILED (too late — it's on a truck). A buggy state machine allows both.
2. **The skip.** QUARANTINED → SHIPPED is the tempting shortcut (skip the release step to ship faster). It must be a compile-time or scan-time error, not a runtime surprise.
3. **Terminal finality.** DELIVERED and SPOILED have no outgoing transitions.

```fsharp
// The trap an AI agent will write:
let ship (s: Shipment) =
    { s with Status = Shipped; ShippedAt = Some Instant.now() }
    // ❌ No guard. Ships from QUARANTINED. FsAssay must catch the missing
    //    match on s.Status. The correct version forces:
    //    match s.Status with
    //    | Released -> Ok { ... }
    //    | other    -> Error (InvalidTransition (other, Shipped))
```

---

## The Trap Table — What Breaks If the Tool Is Buggy

| # | Trap | Buggy CanonFlow does | Correct CanonFlow does |
|---|---|---|---|
| 1 | `^…$` regex | Admits `"AB-123456\n"` (looser than PG → bad writes) | Emits `\A…\z`, rejects |
| 2 | `min_temp < max_temp` | Skips it (single-column only) → silent gap | Row validator, `Stronger`/`Exact` |
| 3 | `expires_at > CURRENT_DATE` | Bakes `DateTime.Now` into a pure constructor | Injects clock, marks `Conditional` |
| 4 | `UNIQUE(batch_no)` | Marks `Unsupported` → strict release impossible | Marks `DatabaseOwned`, passes |
| 5 | FK `batch_id` | Marks `Unsupported` | Marks `DatabaseOwned` |
| 6 | `metadata JSONB` | Maps to `string`, claims `Exact` | Maps to `JsonDocument`, marks `MLossy` |
| 7 | `spoilage <= quantity` | Skips | Row validator |
| 8 | `invoice_total = …` | Skips or generates wrong arithmetic | Row validator, differentially tested |
| 9–10 | Temporal ordering | Skips | Temporal validator |
| 11 | `status != 'SPOILED' OR …` | Skips | State × temporal validator |
| 12 | **Nullable-in-CHECK** | **Rejects NULL notes (stricter than PG → breaks reads of resident data)** | **Kleene `Unknown` → `admits true`** |
| 13 | `length()` on emoji | Counts UTF-16, diverges on surrogate pairs | `EnumerateRunes()` |

| # | Trap | Buggy FsAssay does | Correct FsAssay does |
|---|---|---|---|
| 14a | `let mutable spoilage` in domain | Misses it | FSA1001, blocks |
| 14b | `releasedAt.Value` | Misses it | FSA1002, blocks |
| 14c | `failwith "expired"` | Misses it | FSA1006, blocks |
| 14d | `NpgsqlConnection` in domain | Misses it | FSA2022, blocks |
| 14e | Hard-coded conn string | Misses it | FSA-SEC01, blocks |
| 14f | `sprintf` SQL | Misses it | FSA-SEC02, blocks |
| 14g | QUARANTINED→SHIPPED | Misses it | State machine rule, blocks |
| 14h | AI dead-code helper | Misses it | FSA-AI01, warns |

---

## Pass/Fail Criteria

**CanonFlow passes ColdProof iff:**

$$\text{All 13 schema traps classified correctly} \;\wedge\; |\textsf{Unsupported}| = 0 \;\wedge\; \text{differential test agrees with PG on 10{,}000 random rows}$$

The differential test is the oracle. For each generated validator:

```fsharp
let ``ColdProof validators agree with PostgreSQL`` (conn: NpgsqlConnection) =
    let prop (notes: string option) (batchNo: string) =
        let fsharp = ShipmentValidator.admits { Notes = notes; BatchNo = batchNo; ... }
        let pg = tryInsertWithSavepoint conn "shipments" [...]
        fsharp = pg   // ← if this fails on a NULL-notes row, trap 12 fired
    Check.One({ Config.QuickThrowOnFailure with MaxTest = 10_000 }, prop)
```

**FsAssay passes ColdProof iff:**

$$\text{All 8 code traps caught} \;\wedge\; \text{0 false positives on the clean variant} \;\wedge\; J = \langle\text{Complete}, \text{Conformant}\rangle \text{ only when clean}$$

And the **meta-test**: FsAssay must report `⟨Partial, Unknown⟩` — not `Pass` — when CanonFlow's evidence is missing. A `Pass` issued while `LOSS.md` has unguarded rows is a failure of the whole system, not a success.

---

## Build Plan — 10 Days

| Day | What | Gate |
|---|---|---|
| 1 | Schema + `docker-compose` (pinned PG 16) + seed data | `psql` runs clean |
| 2–3 | CanonFlow front end: pgsqlparser decode → IR for this schema | IR roundtrips |
| 4 | Projections: `Rows.fs`, `Domain.fs`, `Validators.fs` (Kleene-aware) | Compiles |
| 5 | Fidelity classification + `EVIDENCE.md` + `LOSS.md` | All 13 traps classified |
| 6 | Differential tests (10k rows, savepoint harness) | Agrees with PG |
| 7 | Domain + state machine (clean version) | FsAssay: 0 violations |
| 8 | **The dirty branch**: introduce all 8 code traps | FsAssay catches 8/8 |
| 9 | False-positive sweep on clean code + `⟨Partial,Unknown⟩` meta-test | 0 FP, correct verdict |
| 10 | CI: CanonFlow → FsAssay → differential → verdict. Write the report. | Green, honest |

Day 8 is the heart of the test. You **deliberately write the bad code an AI agent would write**, and count how many FsAssay catches. Fewer than 8 means FsAssay isn't ready. 8 of 8 with false positives on the clean branch means it's overfit.

---

## What Failure Looks Like (Honest)

The most likely outcomes, in order:

1. **Trap 12 fires.** The Kleene logic isn't wired through the generated validators, and a NULL `inspector_notes` row is rejected. This is the single most probable failure and the most valuable one to find — it's the silent, inverted kind.
2. **Traps 4/5 misclassified.** `UNIQUE`/FK land in `Unsupported` and the strict gate can never pass. Fix is one fidelity case; finding it is the point.
3. **Trap 13 fires on emoji.** `length()` divergence. One-line fix, but only the differential test surfaces it.
4. **FsAssay catches 6/8.** Probably misses the state-machine skip (14g) and the dead code (14h) — those need the graph pass, which may not exist yet. That's a real finding about the roadmap, not a defeat.

Any of these, **caught and recorded in `LOSS.md`, is a pass for the litmus test.** The test failing means the tools *pretended* to pass.

---

## Why Not Something Simpler

A todo app has no temporal constraints, no nullable-in-CHECK, no money, no ENUM, no irreversible state machine. CanonFlow passes it trivially and you learn nothing. A full ONDC marketplace takes three months and buries the signal in scale.

ColdProof sits exactly in the middle: **small enough to build in ten days, hostile enough to break the tools where they're actually weak.** And when it passes — when the differential test agrees with PostgreSQL on ten thousand rows, when FsAssay catches all eight traps with zero false positives, when the verdict reads `⟨Complete, Conformant⟩` and every line of `LOSS.md` is accounted for — you'll have something no README can claim:

**A receipt.**

$$\blacksquare$$