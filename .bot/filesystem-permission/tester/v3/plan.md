# Tester v3 — plan

## Scope
Validate test quality for coder v3 on `filesystem-permission`. Coder v3 is a
small version: it fixed codeanalyzer v2 #2 (a one-line behavioral change to
`PLangFileSystem.ValidatePath`) and deferred v2 #1. I review the whole branch's
test posture but weight the v3-specific change heaviest — review-driven code is
the highest-risk category for missing coverage.

## Steps
1. Read coder summary + v3 report. Confirm what v3 actually changed.
2. Note process state: no `baseline-tests.md` in any coder version dir.
3. Clean rebuild + run both suites (C# `dotnet run --project PLang.Tests`,
   PLang `plang --test` from `Tests/`). Separate regressions from
   pre-existing failures.
4. Coverage run on the C# suite → `coverage.json`.
5. Test-quality analysis — deletion test, parameter-swap test, false-green
   hunt — across the permission test files.
6. Read every permission `.test.goal` + `.pr`; confirm step text matches
   modules and the goal actually exercises its named scenario.
7. Write `test-report.json`, `result.md`, `verdict.json`, `coverage.json`,
   update `summary.md`, report.json.

## Focus areas (highest risk first)
- **v3 change** — `ValidatePath:227` `OrdinalIgnoreCase`→`Path.RootComparison`.
  Is there a test that fails if it is reverted?
- **Move/Copy** — does any test verify `MoveTo` actually removes the source?
- **PLang `.test.goal` false greens** — goals named after Stage 5 scenarios
  that may only do trivial in-root round-trips.
- Storage-layer assertion strength (`IdempotentAdd`, `TwoHomes`).
