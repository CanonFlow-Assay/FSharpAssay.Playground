# PostgreSQL wallet fixture

`Wallet.Domain` validates a debit: normalized reference of at most 64 characters,
positive amount, cent precision, representable storage amount and an inclusive
request limit. `Wallet.Adapter` owns SQL and HTTP; `Wallet.Api` is a small loopback
console host. The registered suite exercises the actual adapter with PostgreSQL.

One transaction locks the idempotency key, compares any prior wallet/amount intent,
conditionally reduces the wallet balance, records the response and appends one
outbox event for an accepted debit. Identical replays return the original result;
changed intent conflicts. Rejected insufficient-funds responses are retained.
Database checks and conditional updates protect against negative balances under
the tested competing requests. The outbox is storage, without a delivery worker.

HTTP accepts POST `/debit` with `reference`, `wallet` and `amount` JSON fields and a
configured bearer token. It validates authorization before persistence, limits the
body to 8 KiB and returns registered `reference`, `status`, `balance` fields for an
accepted request. JSON/validation errors return bounded error objects; unexpected
database failures propagate for diagnosis. This is a single-request fixture, not
a production identity, TLS or availability design.

Use `python3 eng/qualify.py --output artifacts/qualification` from the root. The
harness provisions a dedicated `fsplayground_` database and owns cleanup. The Store
refuses other database names; reset helpers truncate only fixture tables. Do not
point the example at application data. The standalone host requires
`FSPLAYGROUND_POSTGRES` and `FSPLAYGROUND_TOKEN`, listens on `127.0.0.1:5088`, and
does not create balances or expose a reset endpoint.

The 22 cases cover validation bounds, exact balance, insufficient funds, durable
state, identical/changed replay, pre-cancellation, concurrent requests, authorization,
wire fields, body size and malformed JSON. They do not establish mid-query SQL
cancellation, process-crash recovery or exactly-once external event delivery.
