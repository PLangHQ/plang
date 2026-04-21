# v5 Review Summary (what tester v5 flagged, for v6 re-verification)

Tester v5 landed on **needs-fixes** with 10 findings:

## Major (must-discharge)
- **F1** — `TestRunSubscribesAfterActionForCoverage.test.goal` ended with `assert 1 equals 1`. Tautology — the test observed nothing. Suggested: delete or reach into `Testing.Coverage`.
- **F2** — `TestRunIsolatesMemoryStackBetweenTests.test.goal` asserted `%isolation_probe% is null` but no other test wrote `%isolation_probe%`. No source of pollution. Suggested: pair-fixture with polluter that runs first.
- **F3** — 3 Report tests (`WritesJunitXml`, `IncludesCoverageTables`, `RendersFailureWithVariables`) had IDENTICAL bodies: `read system/test.goal + assert contains 'test.report'`. Inflated green count. Suggested: make each distinct.
- **F4** — No test covered `condition.elseif` matching branch (b=1). Suggested: add `TestConditionElseIfMatchesRecordsBranchIndex1.test.goal`.

## Minor (optional nits — not blocking)
- **F5** — `TestAssertFailureSnapshotsVariables` only asserts `%hasFail% is true`. Should drill into `%results[0].Error.Variables.foo%`.
- **F6** — `TestTagOutsideTestIsNoOp.test.goal` name lies (runs INSIDE runner, exercises accumulate branch, not no-op). Suggested: rename or delete.
- **F7** — `OrchestrateBranchCoverageTests.cs` RegisterCoverageProbe uses old `IsCondition && IsFirstConditionInStep` filter; production now uses `IsIfHead`.
- **F8** — No C# unit test for `Operator.NormalizeTypes` enum↔string path.
- **F9** — `TestSystemTestGoal{NoForeach,RunsAllDiscovered}` had 90% overlapping bodies.

## Latent runtime bug (out of scope for tester)
- **F10** — `test.run timeout=1` maps Status.Timeout correctly but does not cancel child's `timer.sleep`. Wallclock 5008ms instead of ~1000ms. Coder self-flagged.

## Coder v4 response
Per `coder/v4/summary.md`:
- **F1**: deleted the tautology test + .pr (net -1 .test.goal)
- **F2**: replaced with `_isolation/AIsolationPollute.fixture.goal` + `_isolation/BIsolationProbe.fixture.goal` pair under parallel=1
- **F3**: added observable scalar Properties to `test.report` Data (`format`, `reportPath`, `summaryPass`, `summaryFail`, `variableSnapshotCount`) and rewrote each Report test to assert distinct scalars against fixtures
- **F4**: added `TestConditionElseIfMatchesRecordsBranchIndex1.test.goal`
- **F5-F9**: deferred (explicitly per tester v5 scoping)
- **F10**: deferred (coder self-flagged, belongs to follow-up)

Coder also fixed an unrelated `Integration/` rename: `TestSystemTestGoalNoForeach.test.goal` → `TestSystemTestGoalDoesNotUseForeach.test.goal` and `TestSystemTestGoalRunsAllDiscovered.test.goal` → `TestSystemTestGoalIncludesAllThreePhases.test.goal` (last commit `ea7aeb85` by test module).

New latent issues coder surfaced:
- **L1** — Goal-relative path resolution inside child Apps is broken. Workaround: test.report exposes absolute `reportPath` on Properties.
- **L2** — `assert.contains Value/Container` is backwards from natural language; builder LLM flips them. Workaround: Report tests use `assert ... equals` on scalars.
