# Historical evidence

These are actual observed failures and mutation outcomes from the local import
exercise. Their source/candidate identities predate the final published commit.
They establish the recorded observations only. Current clean-candidate receipts
are produced by CI and uploaded as artifacts.

- Initial imported solution: build failed on the machine-specific NuGet feed.
- Initial enterprise acceptance: 30 executed, 14 passed, 16 failed. The original
  acceptance source and source bindings are retained alongside the TRX.
- Additional identifier consistency case: 31 executed, 30 passed, one failed.
- Old packaged FsAssay: ToolFailure on the valid F# alias Address, retaining the
  application failures. This is not a behavioral Fail or a successful analysis.
- Original Webuy schema: PostgreSQL rejects cross-table CHECK subqueries.
- Final wallet probes: four behavioral mutations killed by expanded tests, one
  disclosed wording survivor, one invalid syntax mutation. The weak suite executed
  one unchanged normalization test; the expanded suite executed all 22 cases.
- Dependency audit: actual NuGet vulnerability query across the 32-project solution,
  with no affected package entries returned. This is a dated advisory result, not
  proof that the application or retained JavaScript frontend is secure.
