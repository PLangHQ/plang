# coder — runtime2-callstack

## v1 — 2026-05-02

Implemented all 11 phases of the architect's callstack plan and filled the
test-designer's contract. Result: AsyncLocal-rooted Call tree owned by
App.Debug, OBP-shaped Call.@this with Caller/Cause/Children navigation,
fork-safe parallel execution, fine-grained per-flag capture (timing/diff/
deepDiff/tags/history), App.Errors @this with AsyncLocal-flowed %!error%,
recovery via Cause linkage with Handled flag, Variables collection events,
goal-boundary cycle detection using PrPath identity, dead disposal infra
deleted. C# test contract pinned: 2580/2580 pass. PLang test goals written
but blocked on a pre-existing builder issue (TypeMismatch in validateResponse
that pre-dates this branch, under investigation). Details in `v1/summary.md`.
