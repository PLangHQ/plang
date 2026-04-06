# Review of Tester v8 Findings

## What v8 reported

Reviewed coder v5 PLang tests + coder v6 cross-concern fixes. 1390 C# tests pass. Resolved v7 findings #2 (GetChild depth integration) and #3 (fromJson depth error). Two open:

| # | Severity | Issue | Status |
|---|----------|-------|--------|
| 1 | **Major** | ResolveVariablesInPath cycle detection completely untested | Open since v7 |
| 2 | Minor | Clr() depth boundary not tested at 20/21 | Open since v7 |
| 3 | Minor | `JsonDepthExceeded` error key zero coverage (dead code) | Observation |

Plus carry-forwards from v6: thread safety (major), inner context (minor), numeric widening (minor).

## How coder v7 responded

Test-only changes — no production code:

1. **Finding #1 (Major, blocking)**: Added `VariablesCycleDetectionTests` class with 2 tests — one using reflection to pre-seed `_resolvingVars` (verifying guard fires), one verifying no ThreadStatic leakage between calls.
2. **Finding #2 (Minor)**: Added 2 boundary tests — `Clr_ExactlyAtMaxDepth_Resolves` (20 nestings) and `Clr_OneOverMaxDepth_ReturnsNull` (21 nestings).
3. **Finding #3 (Observation)**: No action — dead code acknowledged.

1394 tests pass (4 new).
