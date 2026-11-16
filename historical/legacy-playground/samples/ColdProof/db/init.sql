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
