# First complete packaged FsAssay run

These observations describe the initial .NET 10 import at `5c8bb45`. The current
qualification runs the .NET 11 solution and records a separate standalone receipt
for files outside its project graph. See [qualification](qualification.md).

The clean-candidate run loaded all **32 projects** and analyzed all **52 eligible
F# files**. No project was skipped or unsupported; no compiler-incomplete file,
missing evidence or tool failure remained. Registered tests executed **31 + 14 + 22**
cases with no failures or skips. `verify` produced **Pass / authoritative=true**.
`check` produced **Inconclusive / authoritative=false**, retaining all three
required-test-not-run reasons.

The run emitted 705 observations from 14 rule IDs:

| Rule | Count |
|---|---:|
| FSA-TDD01 | 325 |
| FSA-F04 | 115 |
| FSA-AI10 | 68 |
| FSA-AI11 | 52 |
| FSA2022 | 44 |
| FSA-TDD02 | 35 |
| FSA-C09 | 26 |
| FSA-TDD03 | 14 |
| FSA-C06 | 7 |
| FSA-P02 | 7 |
| FSA-C01 | 5 |
| FSA-C10 | 3 |
| FSA-AI18 | 2 |
| FSA-SEC13 | 2 |

These are experimental or prototype observations. **No blocking or advisory rule
is admitted.** The fixture does not relabel this inventory as 705 confirmed defects
or silently suppress it to create a clean report. Test-side effects and framework
interop need context; individual findings still need review.

The two SEC13 observations carry the synthetic path `Architecture` and `file-scope`
identity. Their location is insufficient to establish an exploitable SSRF issue.
They remain visible as a rule-location quality gap, rather than being presented as
confirmed application vulnerabilities or removed from the receipt.

The original type-alias crash is preserved in `docs/evidence/alias-tool-failure.json`.
Its repair is regression-tested in the engine; the current consumer pins that
repair and the clean-host qualification-feed follow-up. Raw current reports are
generated under the requested output directory and uploaded by CI for its exact
commit. The package archive hash is recorded per run, not assumed identical across
all build hosts.

For agents, this gives a concrete loop: inspect a finding, identify the actual
requirement and boundary, add an acceptance case, execute a targeted mutation, then
read fresh receipts. An analyzer observation alone does not close that loop.
