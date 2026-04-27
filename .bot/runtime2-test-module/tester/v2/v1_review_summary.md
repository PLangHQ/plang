# Review Summary — Test-Designer's Review of Tester v1 Plan

**Reviewer:** test-designer  
**Date:** 2026-04-16

## Add (scope gaps)
1. **`test.skip` action** — no mechanism for a test to declare itself skipped (e.g., "requires network")
2. **Per-test timeout** — no hung-test guard; one deadlocked test starves the parallel pool
3. **Tag/filter system** — 142 test files, no way to run subsets (`--tag identity`)
4. **JUnit XML output** — CI lingua franca; bespoke JSON forces custom adapters

## Push back / clarify
5. **Use AfterAction event for coverage, not a special hook** — Action.RunAsync already fires lifecycle events; subscribe to those instead of patching the dispatcher
6. **Isolation unit is App, not Actor** — "fresh Engine" must mean fresh `App.@this`; Actor-only reset leaks SQLite/identity/settings
7. **Variable dump must happen inside the assert handler** — step-scoped variables are popped on step exit; by the time runner sees failure, frame is gone. Snapshot belongs in `DefaultAssertProvider.Equals` at failure moment
8. **Existing 142 tests use `Start`, not `Test*`** — goal naming convention mismatch; pick file-is-the-test or migrate all 142
9. **Discovery + iteration must be C#** — PLang `foreach` loop was the bug that silently skipped 86 tests; test runner's main loop must not be PLang foreach

## Trim / defer
10. **`.pr` snapshot testing → builder, not test runner** — belongs in `plang build --check-stability`, not `plang --test`
11. **Mutation testing** — defer but preserve the "PLang .pr is cheap to mutate" insight
