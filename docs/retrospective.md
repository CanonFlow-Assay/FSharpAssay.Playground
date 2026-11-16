# Retrospective

The source collection's strength is the connection between storage constraints,
typed domain behavior, generated client validation and adversarial cases. Keeping
those together gives agents concrete cross-layer examples and useful failure inputs.

The import exposed problems that the original root solution could not see: it
listed two projects out of 24. A complete solution first found portability and
compilation errors. New behavior cases then found 16 failures among 30 cases;
another case exposed the identifier/column contradiction. Native PostgreSQL found
the illegal cross-table CHECK constraints. Jest execution found mock-drill's broken
test wiring and duplicated imports. These were different evidence stages and
required different repairs.

FsAssay also failed on a valid type alias. The raw ToolFailure was retained, a
compiler-backed regression reproduced it, and the engine was repaired while
preserving enclosing boundaries. All 121 engine tests passed. Clean-host CI then
exposed a separate qualification-feed problem concealed by a developer cache; the
consumer now declares its application feed independently of the tool-only feed.
The consumer harness also isolates the outer workflow identity from its own local
Git commits. All seven engine qualification gates pass under a simulated CI identity.

The implemented additions provide 67 focused acceptance cases, alongside the
53 retained .NET cases and 33 Jest cases across 13 clients. The retained counts
include placeholders. Thirteen initial schemas load in disposable databases; SQL
behavior probes exercise ten rejected writes and valid boundary writes. This
does not establish every generated contract or every business workflow.

Four wallet regressions survive a deliberately weak one-case suite and are killed
by the expanded 22-case suite: exact limit, positive amount, replay intent and wire
field name. The wording survivor is disclosed because that exact string is outside
the registered contract. The invalid syntax case is retained as invalid rather
than treated as a killed requirement. Harness attempts that selected zero tests
failed visibly before the final selective filter was corrected.

Useful next work should answer a concrete requirement: real configured signature
verification, reservation reconciliation, cross-layer generator equivalence,
crash/outbox delivery behavior or a particular frontend workflow. Broad “certified”
labels would conceal the current evidence gaps. The present package consumer,
source inventory, raw reports and negative controls provide a repeatable starting
point for those changes.
