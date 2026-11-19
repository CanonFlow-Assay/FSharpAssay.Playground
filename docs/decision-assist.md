# Decision Assist status

The F# playground uses the pure reducer from the pinned FsAssay producer's
`playground/decision-assist/Decision.fs` at `eae0114a9e658a63a88dd370bc8a24c6bb8ad909`.
Its 17 routing cases are registered with FsAssay as required tests. The reducer
requests context for missing, stale or empty evidence and routes failed baselines,
invalid candidates, killed candidates and survivors separately.

Run `python3 eng/mutations.py --output artifacts/mutations` after the Release
build. The wallet harness compares unchanged weak and expanded test selections
against six isolated candidates. `decision.json` is emitted from the native F#
reducer using the executed counts. Four killed candidates need no investigation,
the wording survivor requests review, and the compiler-invalid candidate asks for
repair. Source hashes, raw TRX and command exits remain alongside the report.

| Surface | Current scope |
| --- | --- |
| FSharpAssay.Playground | Offline deterministic routing from real wallet mutation evidence. No provider adapter. |
| FSharpAssay producer playground | Independent offline six-case M5 corpus with 17 routing controls. Its SDK is 11.0.100. |
| CSharpAssay.Playground | Offline experiment and synthetic scenarios. Jev is an opt-in provider for eligible advice. Five historical live requests were exercised before the missing-context guard. Three of four clear routing cases matched expectations. The revisit makes no live requests. |

The historical Jev results are a small integration trial. Resolved model revision,
held-out human adjudication and provider quality remain unmeasured. The C# adapter
keeps incomplete evidence and insufficient context on local routing. Advice cannot
change verification authority or grant Pass. [C# provider evidence](https://github.com/CanonFlow-Assay/CSharpAssay.Playground/blob/9d45c0b/samples/50-decision-assistance/README.md#earlier-local-validation-on-2026-10-18).
