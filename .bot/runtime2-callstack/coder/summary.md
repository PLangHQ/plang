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

## v2 — 2026-05-04

Closes the branch. Merged the runtime2-source-resolution sub-branch
(959cdd36 "stored values are values, no recursion"); removed the
dd7bf37e infra-skip in SubstitutePrimitive after confirming it became
redundant AND regression-causing post-merge; fixed two callstack PLang
tests (Audit count expectation + HandledFlagFalse impl); added the
LlmFixer-flow regression test; landed Phase 11 (CallChainRenderer with
recursion compression + Cause annotation, error-report side only —
Children-walk / flamegraph deferred per Ingi). Plus housekeeping:
gitignore traces + junit, CLAUDE.md CLI examples updated, dropped the
now-stale source-resolution-problem.md. C# tests: 2623/2623. PLang
tests: 181/181. Branch ready to merge. Details in `v2/summary.md`.
