# v1 Review Summary

## Reviewers
- **Codeanalyzer v1**: PASS — all 6 engine files clean, no OBP violations
- **Tester v1**: FAIL — 8 findings (2 critical, 4 major, 2 minor)

## Critical Findings
1. **C# tests don't compile** — `DiscoverAsync` made private in `Setup/this.cs` but 3 tests in `SetupTests.cs` (lines 305, 331, 346) still call it. All 1500 C# tests cannot run.
2. **3 PLang tests fail** — ErrorProps, ErrorInHandler, RecursionDepthLimit missing `onError` in .pr files (builder limitation, deferred).

## Major Findings
3. **ConditionCompound** — condition.if takes `bool` but test expects expression evaluation. Deferred (in todos).
4. **CacheDynamicKey** — .pr has wrong assertion value (`content2` not `content1`). Builder issue.
5. **Steps.RunAsync return value change** — no C# test. Reverting to `Data.Ok()` would break no C# test.
6. **Goals PrPath keying** — no C# test for collision prevention or name-search fallback.

## Minor Findings
7. **Setup convention discovery** — no test verifies the 2-path convention.
8. **Assertion swallowing** — edge case with `on error ignore` on assertion steps. Acceptable tradeoff.

## Action Required
- Fix #1 (compilation) — update SetupTests to use RunAsync
- Fix #5 — add C# test for Steps return value propagation
- Fix #6 — add C# test for Goals PrPath keying
- Fix #7 — add C# test for Setup convention discovery
- #2, #3, #4 — deferred (builder issues / in todos)
- #8 — acceptable, document only
