# Tester v5 — plan

Reviewing coder v5 (response to codeanalyzer v2 findings N1–N3) on
`path-polymorphism`. codeanalyzer v3 verdict: PASS/CLEAN. My lens is test
*honesty*, not code correctness — would these tests fail if the implementation
were subtly wrong?

## Baseline

Coder recorded the v5 baseline inline in `coder/v5/plan.md` (no separate
`baseline-tests.md` — the v3 one exists, v5 reuses the inline form). Baseline:
C# 2881→2882, plang 203/203/0 stale.

## Method

1. Clean rebuild (stale-binary trap) — done, 0 errors.
2. Re-run both suites — C# **2882/2882**, plang **203/203/0 stale**. Matches
   coder/codeanalyzer. No regressions vs baseline.
3. Read the path-polymorphism C# test suite (`PLang.Tests/App/Types/PathTests/`)
   — contract base, verb round-trips, scheme registry, handler shapes.
4. Deletion-test the three v5 review-driven fixes (N1/N2/N3): if the fix were
   reverted, would a test go red?
5. Check plang `.goal` coverage for the headline F3 semantic
   (`if %path% exists`).

## Focus — review-driven code is highest risk

codeanalyzer v2 asked for three changes. N1 got a dedicated oracle. N2/N3 are
the ones to scrutinise — the test for the exact change a reviewer requested is
the most likely to be missing.

## Output

`result.md`, `test-report.json` (branch root), `verdict.json`, `coverage.json`.
