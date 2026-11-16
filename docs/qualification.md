# Reproducing qualification

Requirements: Git, exact .NET SDK **10.0.301**, Python 3.12+, Node 26/npm, Docker
with a running Linux daemon and network access for locked dependency restore and
the pinned PostgreSQL image. `global.json` disables SDK roll-forward. No global
FsAssay installation or existing database is required.

```sh
git clone https://github.com/CanonFlow-Assay/FSharpAssay.git ../FSharpAssay
git -C ../FSharpAssay checkout eae0114a9e658a63a88dd370bc8a24c6bb8ad909
python3 eng/provenance.py
python3 eng/prepare.py --engine ../FSharpAssay
python3 eng/qualify.py --output artifacts/qualification
python3 eng/clients.py --output artifacts/clients
python3 eng/mutations.py --output artifacts/mutations
```

Run from a clean committed playground checkout. Each output directory must be new.
`.producer`, `.packages`, `.tools`, artifacts, dependency installs and build outputs
are ignored. Use `--development` on `eng/qualify.py` while editing to execute build,
legacy, schema, dependency and acceptance checks without asking for commit authority.

`prepare.py` rejects a dirty or mismatched engine checkout, restores locked packages,
packs the CLI, installs through a local-only feed and a package-hash-specific cache,
and records producer/archive identity. The package version remains 1.0.4; the pinned
source revision and archive identity distinguish this regression repair from older
archives carrying the same version.

`qualify.py` checks solution completeness, performs locked restore/build, runs all
14 imported .NET test projects, starts an owned disposable PostgreSQL 18.7 container,
replays 13 initial schemas in separate databases and executes SQL behavior probes.
It queries current NuGet advisories for all direct/transitive .NET dependencies and
rejects incomplete results or affected package entries. This is dated advisory
evidence; JavaScript dependency auditing and general application security are outside
that claim. It executes 31 enterprise, 14 pattern and 22 wallet acceptance cases. It then runs
the packaged FsAssay `verify` and `check` against the root solution.

The fixture publishes only a random loopback port, generates an ephemeral password
without placing it in command arguments, and removes only its own container in a
`finally` block. It never connects to a user-provided database. Do not upload Docker
inspection output or process environments containing fixture credentials.

`verify` must exit 0; `check` must exit 2 because required tests have not run in that
mode. The authority policy has **zero blocking and zero advisory admissions**.
Experimental findings remain observations; a passing receipt is bounded execution
and policy evidence, not a business or security certificate.

`clients.py` installs from each of the 13 retained client locks with lifecycle
scripts disabled, and runs all Jest suites. It rejects missing or skipped tests.
The imported Next.js frontend is outside this scope.

`mutations.py` builds isolated copies and runs unchanged tests against six mutations.
Its weak before-suite contains one normalization test; the expanded suite contains
all 22 wallet cases. Four behavioral mutations should survive the weak suite and
be killed by the expanded suite. A wording mutation survives because the exact
validation wording is outside the registered contract. A syntax mutation is
invalid, never counted as a killed acceptance case. Logs, TRX, source hashes and
the deterministic routing decision remain in the output directory. No LLM provider
evaluation is implied.

CI uploads raw receipts, reports, TRX and logs for its exact checkout. Local evidence
under `artifacts/` is intentionally untracked. Historical red evidence is retained
in `docs/evidence/`; its candidate identities refer to the local import baseline,
not a claim about the final published candidate.
