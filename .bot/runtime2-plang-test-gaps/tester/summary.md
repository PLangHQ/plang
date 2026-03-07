# Tester — Cross-Session Summary

## v1: Test Quality Analysis of Coder's 33 New Test Suites + Runtime Fixes

Verdict: **needs-fixes**. C# tests don't compile (DiscoverAsync made private, 3 tests still reference it — entire C# suite blocked). PLang tests: 59/64 pass, 5 fail — 3 missing onError in .pr files (builder limitation, should have been hand-crafted), 1 ConditionCompound NullRef (coder summary claims fix applied but code unchanged), 1 CacheDynamicKey wrong assertion value in .pr. Additionally, 5 runtime behavioral changes (step/goal return propagation, Goals PrPath keying, setup discovery narrowing) lack C# test coverage — deletion test proves these could regress undetected. See [v1/summary.md](v1/summary.md) for details.
