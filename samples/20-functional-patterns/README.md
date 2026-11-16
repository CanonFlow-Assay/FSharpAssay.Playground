# Functional patterns and owned effects

`Patterns.Domain` expresses input bounds with private smart constructors, closed
alternatives with DUs, independent validation with an error list, a strategy as a
function parameter, audit output as data, units of measure, recursive menus, event
folds and active-pattern parsing. These are idiomatic F# counterparts to useful
design patterns; this is not an implementation of every GoF pattern.

`Patterns.Shell` demonstrates independent Task-based work over a bounded channel.
For concurrency N (1..64), it owns one producer, N consumers and a queue of N items.
Overflow waits. Consumers retain input indices, and the coordinator publishes an
ordered array only after all tasks finish. Normal completion drains the channel;
failure cancels related work and observes all owned tasks. Caller cancellation is
propagated. No retry, replay or transactionality is implied for effectful work.

The bound covers item count and running calls. Generic item/result sizes are not
bounded, and the final result dictionary/array retains all outputs. Callers need
their own record-size limits and workload measurements before using this design
for a memory budget. The example uses small integers. It does not publish partial
results on failure or guarantee cancellation of a noncooperative callback.

Four async acceptance cases exercise out-of-order completion, worker capacity,
a failed consumer with a saturated producer, and cancellation. Ten pure cases
exercise the modeling examples. Run the suite through `eng/qualify.py` from the root.
