# Tester v5 — Plan

## What the coder shipped (v3)
F1 discharge: all 19 `.test.goal` files now Pass (was 16 stubs + 2 rolled-back).
Architectural addition: `condition.elseif` + `condition.else` as first-class actions.
Collateral: `test.tag` returns accumulated UserTags; `Operator.NormalizeTypes` gets enum↔string.
Self-declared weakness: `TestAssertFailureSnapshotsVariables` asserts `Status=Fail` only, not the Variables snapshot; `TestTagOutsideTestIsNoOp` runs the accumulate branch, not the no-op branch. Self-declared latent bug: `test.run timeout=1` maps to `Timeout` status but doesn't actually cancel child `timer.sleep` (wallclock 5s, not 1s).

## Scope of this review

### Run tests
1. C# suite: `dotnet run --project PLang.Tests` — confirm coder's `2271/2272` claim.
2. PLang suite from project root: `plang --test` — confirm all 19 Pass, 0 Stale.
3. C# coverage: `coverlet` on the four touched areas (`condition/if.cs`, new `elseif.cs`/`else.cs`, `test/run.cs`, `test/tag.cs`, `Operator.cs`).

### Test-quality analysis (core job)

For each of the 19 PLang `.test.goal` files, answer:
- Is this verifying intent or implementation?
- Deletion test: if I delete the production code line X, would this test fail?
- Is the assertion in a place that's reachable by the production path?

Specifically suspect:
- **`TestRunIsolatesMemoryStackBetweenTests`** — asserts `%isolation_probe% is null` at entry, but no *other* test writes `isolation_probe`. If isolation breaks, would anything change in the observable state? Likely false-green.
- **`TestRunSubscribesAfterActionForCoverage`** — the comment says "observable regression guard", but the test doesn't actually reach into the coverage table to assert `file.read` / `output.write` were recorded. `assert 1 equals 1` is a tautology.
- **`TestTagOutsideTestIsNoOp`** — coder self-flagged this. Tests the accumulate branch, not the no-op branch. Misleading name.
- **`TestAssertFailureSnapshotsVariables`** — asserts `Status=Fail` via `list.any`, but doesn't observe the AssertionError.Variables payload. The whole point of the "Snapshots" tests is the Variables map.
- **Structural 5 tests** (`TestSystemTestGoal*`, `TestReport*`) — `read file '/system/test.goal'; assert contains 'test.report'`. A regression that drops `test.report` *logic* but keeps the word present in a comment would pass. These are string-presence tests, not behavior tests.

Spot-check the .pr files:
- Per CLAUDE.md: "Always read the .pr file after build and verify that module, action, and parameters match the step's intent." Every test.goal's .pr must match what the step text said.

### C# tests for new code paths

New code:
- `condition.elseif.cs` — has `Run()` that evaluates and returns bool
- `condition.else.cs` — has `Run()` returning `Data(true)`
- `DefaultEvaluator.Evaluate(Elseif)` — new overload
- `Operator.NormalizeTypes` — enum↔string branch (two new lines)
- `run.cs:L91` filter changed from `IsCondition && IsFirstConditionInStep` to `IsIfHead`
- `test.tag` return change — returns `List<string>` snapshot instead of `Ok()`

Open questions:
1. Is there a C# unit test for `condition.elseif.Run()`?
2. Is there a C# test for the enum↔string normalization in `Operator.NormalizeTypes`?
3. Is there a C# test that the coverage filter now uses `IsIfHead` (not the old `IsFirstConditionInStep`)?
4. Does the `test.tag` return snapshot survive JSON roundtrip when written to `%__data__%`?

`OrchestrateBranchCoverageTests` — check whether the test mirrors the OLD filter (`IsCondition && IsFirstConditionInStep`) or the new one (`IsIfHead`). If it mirrors the old, that's a finding — the test no longer validates the production filter shape.

### Self-declared latent bug
The `timeout=1` not cancelling `timer.sleep` claim. Verify. If true, the Status mapping passes the test but the child App keeps running for 5s. A real runtime bug — not a test-quality issue per se, but worth recording for the next coder.

## Deliverables
- `v5/plan.md` (this file)
- `v5/result.md` — findings + deletion-test analysis per test goal
- `v5/coverage.cobertura.xml` (or `.json`) — coverage output
- `.bot/runtime2-test-module/test-report.json` — structured JSON for downstream bots
- `v5/verdict.json` — `pass` or `needs-fixes`
- `v5/summary.md` — session summary
- `v5/changes.patch` — excluded-.bot diff

## Expected outcome
If the 19 .test.goal files pass and the weak ones I called out above are genuinely weak (not hidden strengths I missed), the verdict is likely `needs-fixes` with minor/major findings — but not blocking. The suite is green at surface level; this is about false-green hunting.
