# tester v2 — fix-stepvartypes-incremental

## Scope

Coder commits 81c9dabfa and e4376de87 address v1 findings F1–F6 (plus a follow-up fix for `DriftCaseArtifactTests` path). Verify:

1. C# build green and full suite passes
2. PLang suite count did not regress
3. New tests actually verify the behaviors they claim — not coarse green-only assertions

## Plan

1. Build PLang.Tests → green
2. Run TUnit binary → expect previously-broken assembly now runs all tests, none failed
3. Read each new test plain, mutation-check by reasoning about what would fail if production logic were inverted
4. Sample-verify the F6 assertion is now `IsEqualTo`, not `Contains`
5. Re-run `plang --test` to confirm 22 → not-worse
6. Write verdict.json, summary.md, test-report.json
