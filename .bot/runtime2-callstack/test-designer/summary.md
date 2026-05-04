# test-designer — runtime2-callstack

## v1 — 2026-05-02

Wrote the test contract for the causal callstack refactor: 95 C# TUnit stubs across 15 files (Call shape, CallStack tree, AsyncLocal forks, Cause linkage, cycle detection, flags, Items, Diff capture incl. memory test, Audit, SnapshotChain, app.Errors.Push scope, Variables collection events, tag action, --debug parse, ServiceError chain) plus 16 PLang `.test.goal` files (one goal per file) under `Tests/App/CallStack/`. P8 (OOM safety) moved to C# memory test; P9 (cancellation) kept on PLang via existing `timeout after` modifier. Removed obsolete tests targeting the deprecated `%!callStack.Depth%` API. All bodies stubbed. Details in `v1/summary.md`. Next: coder.
