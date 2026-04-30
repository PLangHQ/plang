# Review of v2 by codeanalyzer

Codeanalyzer v2 verdict: **FAIL**. Production fixes from v1 (cycle detection, EquatableArray record carrier, dead emission removal, OCE/non-generic comments, raw-string emission, F2/3/6/9/21 cleanups) all verified correct. `plang test` is unblocked. The failure is specifically about the test-gap closure Ingi pushed for in v1.

## What v2 got wrong

| # | Severity | Topic |
|---|---|---|
| 39 | MAJOR | `IncrementalCacheTests` doesn't drive Roslyn — 9 unit-equality tests on the carrier records, not a `CSharpGeneratorDriver` cache-hit test |
| 40 | MAJOR | `NoDeadEmissionTests` heuristic cannot catch the regressions it was named after — `__variables` (decl + 1 LHS, no read) computes `reads=1`, the test flags only `reads<=0`. `__paramData` needs cross-file analysis the test doesn't do |
| 41 | MINOR | Cycle protector keys on raw input string; expanding cycles (`%a%="X-%b%"`, `%b%="Y-%a%"`) still recurse infinitely |
| 42 | MINOR | OCE asymmetry pinned only on App.Run side. Plan promised paired `StepRunAsync_HandlerThrowsOCE_LetsItPropagate`; not delivered |
| 43 | MINOR | 3 of 4 cycle tests assert only `IsNotNull`; value contract not pinned |
| 44 | NIT | `NoDeadEmissionTests` regex restricts to `__`-prefixed fields without enforcing the convention |
| 45 | NIT | Finding 7 (synthetic 1-char diagnostic span) silently dropped from v2 not-taken list |

## What v3 will do

Fix all 7. The production-code fixes from v2 stand; v3 hardens the regression-prevention layer.
