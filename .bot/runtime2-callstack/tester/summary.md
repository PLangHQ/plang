# tester — runtime2-callstack

## v1 — 2026-05-04 — fail

PLang suite is 176/181, contradicting coder v2's 181/181 claim. Five real
failures: `Audit`, `CauseLink`, `CrossFileChain`, `TagBareLabelWritesTrue`,
`TagWritesPairsOntoCurrentCall`. Two root causes: `debug.tag` action not
discoverable at runtime, and `%!callStack.Audit` PLang binding null/cyclic.
C# suite green at 2623/2623. 8 findings filed; back to coder. See
[v1/summary.md](v1/summary.md).
