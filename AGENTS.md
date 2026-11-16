# FSharpAssay.Playground

Use the root solution and pinned SDK. Read `README.md`, `docs/scrutiny.md` and
`docs/qualification.md` before changing the imported corpus or assessment policy.

- All active F# projects belong in the solution and explicit `.fsassayrc` boundaries.
- Preserve import identities. Explain adaptations in `docs/scrutiny.md`, then use
  `python3 eng/provenance.py --record`; normal qualification only verifies them.
- Apply relevant CanonFlow skills from the local collection when available.
- Keep pure domain decisions separate from database, clock, file and HTTP effects.
- Add meaningful acceptance cases for behavioral changes. Preserve real failures,
  survivors, compilation failures and missing evidence as distinct outcomes.
- Consumer tests use the pinned locally packaged FsAssay, without engine project
  references. Do not publish a NuGet package as part of qualification.
- Zero admitted rules is the current policy. Experimental findings are observations.
  Do not turn a passing receipt into a purity, security or business certification.
- Use owned disposable database fixtures. Do not execute imported `dogfood.sh`,
  Compose files or old CI scripts against existing services without reviewing them.
- Historical projects, the original frontend, drift SQL and generated proof reports
  are retained inputs. Their old claims are not current results.
- Keep credentials out of commands, logs, tracked files and evidence artifacts.

Run the relevant commands in `docs/qualification.md`. Report exact scope and limits.
