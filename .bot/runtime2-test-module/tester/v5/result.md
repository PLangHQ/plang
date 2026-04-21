# Tester v5 — Findings

## Run summary

- **C# suite**: 2271/2272 pass. Same pre-existing LLM flake (`Query_ToolCall_LlmRequestsToolAndHandlesError`). Matches coder v3 claim.
- **PLang suite**: 19/19 pass after rebuilding `plang` binary. Initial run failed 5/19 because the installed `plang` binary was stale (16:09) vs the source (21:20) — after `dotnet build PlangConsole`, the run becomes 19/19 green.
- **C# coverage (filtered namespaces)**:

| File | Line | Branch |
|------|------|--------|
| `test/run.cs` | 100% / 100% / 100% / 79.1% (split across handlers) | 100% / 71.9% / 80% / 100% |
| `test/tag.cs` | 100% | 100% |
| `condition/if.cs` | 86.7% / 100% | 71.4% / 95.8% |
| `condition/elseif.cs` | **0%** | **0%** |
| `condition/else.cs` | **0% line** | 100% |
| `condition/Operator.cs` | 90.2% | 76.2% |
| `condition/providers/DefaultEvaluator.cs` | 78.4% | 50% |
| `Goals/.../Action/this.cs` | 71.4–100% | 62.5–100% |
| `Goals/.../Actions/this.cs` | 89.4% / 100% | 83.3% / 100% |

## Deletion-test analysis (per .test.goal)

### Strong (10) — real guards
1. **Condition/TestConditionIfRecordsBranchIndexTrueBranch** — asserts `branchIndex=0`. Delete the `simple.Properties.Set("branchIndex", ...)` line → test fails. ✅
2. **Condition/TestConditionIfRecordsBranchIndexElseBranch** — asserts `branchIndex=2`. Delete `lastResult.Properties.Set("branchIndex", b)` in Orchestrate → fails. ✅
3. **Tag/TestTagAccumulatesUserTagsOnRun** — asserts three tags accumulated. Delete the `UserTags.Add(tag)` loop → snapshot returns empty → fails. ✅
4. **Run/TestRunReportsAssertionFailure** — fixture fails assert; asserts `Status=Fail`. Delete `testRun.Complete(result)` → Status stays Ready → fails. ✅
5. **Run/TestRunEnforcesTimeout** — asserts `Status=Timeout`. Delete the `cts.IsCancellationRequested` → `Timeout` branch in `run.cs:131` → Status becomes Pass (because sleep completes) → fails. ✅
6. **Assert/TestAssertFailureSnapshotsVariables** — asserts `Status=Fail` through AssertSnapshot. Weak discrimination at the Variables level (F5 below), but Status=Fail does discriminate.
7. **Discover/TestDiscoverFindsTestGoals** — asserts count=3. Discover bug that misses one → fails. ✅
8. **Discover/TestDiscoverReportsStaleWhenPrMissing** — asserts `StatusReason='no .pr'`. Change the string → fails. ✅
9. **EdgeCase/TestDiscoverHandlesIcelandicGoalNames** — asserts `EntryGoalName='Próf'`. Encoding break → fails. ✅
10. **Integration/TestSystemTestGoalNoForeach** — asserts system/test.goal contains `test.discover`, `test.run`, `test.report`. Regression-useful text-presence guard. ✅ (barely — F3 below)

### Weak / false-green candidates (9)
See findings F1–F9 below.

## Findings

### F1 (major) — tautological assertion
**File**: `Tests/TestModule/Run/TestRunSubscribesAfterActionForCoverage.test.goal`

```
- read file '/system/test.goal', write to %goalText%
- write out 'coverage probe'
- assert 1 equals 1
```

The comment claims "observable regression guard for the subscriber wiring", but the test reaches nothing observable — it just executes file.read and output.write, then asserts a tautology. If the AfterAction coverage subscriber broke and recorded nothing, this test still passes.

**Impact**: The "coverage subscriber wired" regression claim is vapor.

**Suggestion**: End with `- assert %coverage.module_actions% contains 'output.write'` (or similar) so a subscriber regression fails the assertion. If PLang can't introspect `Testing.Coverage` from a test goal, either expose it via a `test.coverage` action or downgrade the comment to admit this is a coverage-exercise probe only, not a guard.

---

### F2 (major) — isolation test has no source of pollution
**File**: `Tests/TestModule/Run/TestRunIsolatesMemoryStackBetweenTests.test.goal`

```
- assert %isolation_probe% is null
- set %isolation_probe% = 1
- assert %isolation_probe% equals 1
```

Asserts `%isolation_probe%` is null at entry. No **other** test in the suite writes `%isolation_probe%`. So even if the runner silently shared MemoryStack across tests (the regression this test purports to guard against), there'd be no value to leak in — the variable would be null at entry regardless.

**Impact**: The headline "fresh App per test / MemoryStack isolation" claim of the module is not being verified. The C# test `RunActionTests.Run_FreshAppPerTest_IsolationBoundaryIsFileLevel` does verify it (TestA sets, TestB asserts null) — but this PLang test does not.

**Suggestion**: Either delete this test (the C# one covers the case) or restructure as a two-test pair: `PollutePollute.test.goal` writes `%shared_probe%=1`; `ProbeIsolation.test.goal` asserts `%shared_probe%` is null at entry. Both must run in the same suite to exercise isolation. Consider alphabetical ordering so Pollute runs first.

---

### F3 (major) — three identical Report tests
**Files**: `Tests/TestModule/Report/Test{ReportWritesJunitXml,ReportIncludesCoverageTables,ReportRendersFailureWithVariables}.test.goal`

All three tests have IDENTICAL step bodies:

```
- read file '/system/test.goal', write to %goalText%
- assert %goalText% contains 'test.report'
```

Three tests checking the same string. If `test.report` is dropped from `system/test.goal`, you get three failures for one root cause. They add no incremental discrimination beyond the first.

**Impact**: Noise, not coverage. Inflates the green count from 17→19 without adding guardrails.

**Suggestion**: Keep one (rename to `TestSystemTestGoalInvokesReport`). Rename the other two to reflect what they *should* test: e.g., `TestReportWritesJunitXml` should write a fixture file with an expected junit.xml path, run test.report with `Format=junit`, and assert the file exists and is valid XML. Currently it's a name-only.

---

### F4 (major) — no test for elseif-matches (b=1) path
**Gap**: No `.test.goal` or C# test covers the case where the condition.elseif branch *matches* (runs its body, orchestrator sets `branchIndex=1`).

- `TestConditionIfRecordsBranchIndexTrueBranch` covers b=0 (if matches).
- `TestConditionIfRecordsBranchIndexElseBranch` covers b=2 (else matches); elseif is dispatched at b=1 but returns false.
- C# `ConditionIfBranchIndexTests.MultiBranch_SecondBranchMatches_BranchIndexIs1` uses the OLD model (two `condition.if` actions), not the new `condition.elseif` action.
- C# `OrchestrateBranchCoverageTests.MultiActionOrchestrate_InnerElseIfMatches_...` also uses two `condition.if` — not the new `condition.elseif`.

**Impact**: The orchestrator's elseif-matches path (`branchResult = elseIfResult.Value is true`; run body; set `branchLabel = "elseif[b]"`, `branchIndex = b`) is untested in the new architecture. A regression where elseif dispatch succeeds but the body skips (or branchIndex stays 0) would not be caught.

**Suggestion**: Add `TestConditionElseIfMatchesRecordsBranchIndex1.test.goal`:
```
TestConditionElseIfMatchesRecordsBranchIndex1
- set %x% = 7
- if %x% > 10 set %a% = 1, else if %x% > 5 set %b% = 2, else set %c% = 3
- assert %__data__.branchIndex% equals 1
- assert %b% equals 2
```

---

### F5 (minor, coder self-flagged) — Assert test stops at Status level
**File**: `Tests/TestModule/Assert/TestAssertFailureSnapshotsVariables.test.goal`

The name implies the test verifies AssertionError.Variables carries the %foo%=42 snapshot through the runner. But the assertion is `%hasFail% is true` — just that the test failed. A regression that dropped the Variables snapshot but left Fail status intact would pass.

**Impact**: The "Snapshots Variables" feature (Batch 5 headline) isn't actually guarded end-to-end. The C# test `RunActionTests.Run_AssertionFailureInTest_CapturedInResult...` does verify `AssertionError.Variables["score"]=42` — but this PLang test name implies it does too and it doesn't.

**Suggestion**: Either rename to `TestAssertFailureReportedAsFail` (honest), or reach into `%results[0].Error.Variables.foo%` and assert equals `42`.

---

### F6 (minor, coder self-flagged) — misleading test name
**File**: `Tests/TestModule/Tag/TestTagOutsideTestIsNoOp.test.goal`

Name implies "when `test.tag` is called outside test mode, it no-ops." But the test runs *inside* the test runner, so it exercises the accumulate branch. The no-op branch (`currentTest == null`) is unreachable from a .test.goal.

**Impact**: Name falsely claims coverage that isn't there. The no-op branch is covered by the C# unit test `TagActionTests.Tag_OutsideTestMode_NoOps` (presumably — I didn't audit, but the coder described it).

**Suggestion**: Rename to `TestTagAccumulatesWhenCalledInsideTest` (honest — this is what it actually tests). OR delete since `TestTagAccumulatesUserTagsOnRun` also covers the accumulate branch more thoroughly.

---

### F7 (minor) — OrchestrateBranchCoverageTests mirrors OLD filter
**File**: `PLang.Tests/App/Testing/OrchestrateBranchCoverageTests.cs:53-57`

The test subscriber uses `action.IsCondition && action.IsFirstConditionInStep` — but the production filter at `run.cs:91` was changed to `action.IsIfHead`. The test comment at line 18 still says `same filter as run.cs L77-115: action.IsCondition && action.IsFirstConditionInStep` (stale).

**Impact**: If `IsIfHead` logic regresses, this test won't catch it — the test validates the OLD filter shape. The test is still valuable for the Step-propagation + orchestrate guard + DisableChildrenOf fix (d05c138d), but its "production filter" claim is no longer accurate.

**Suggestion**: Update the subscriber to use `action.IsIfHead` and update the comment. Alternatively, add an assertion that `action.IsIfHead == (action.IsCondition && action.IsFirstConditionInStep)` for the classic `condition.if + condition.if` shape, so both filter shapes remain validated.

---

### F8 (minor) — no C# unit test for enum↔string normalization
**File**: `PLang/App/modules/condition/Operator.cs:176-181` (new code)

```csharp
if (left is Enum leEnum && right is string)
    return (leEnum.ToString(), right);
if (right is Enum reEnum && left is string)
    return (left, reEnum.ToString());
```

No C# test covers these branches. Covered end-to-end by three PLang tests (`TestRunEnforcesTimeout`, `TestRunReportsAssertionFailure`, `TestAssertFailureSnapshotsVariables`) which rely on `where Status equals 'Timeout|Fail'`.

**Impact**: Minor — the end-to-end coverage caught the stale-binary run before rebuild. But a unit test would be cheap and provide faster signal.

**Suggestion**: Add `OperatorTests.Equal_EnumLeft_StringRight_NormalizesToEnumName` and the mirror. Single method, four lines.

---

### F9 (minor) — overlapping Integration tests
**Files**: `Tests/TestModule/Integration/TestSystemTestGoal{NoForeach,RunsAllDiscovered}.test.goal`

- `NoForeach` asserts contains `test.discover`, `test.run`, `test.report`.
- `RunsAllDiscovered` asserts contains `test.discover`, `test.run`, and `does not contain 'foreach'`.

Only `does not contain 'foreach'` differs. Otherwise these overlap fully. Consider merging, or making them distinct (one presence-only, one absence-only).

---

### F10 (latent runtime bug, not test-quality; coder self-flagged)
**File**: `PLang/App/modules/test/run.cs:124-130`

`TestRunEnforcesTimeout.test.goal` takes **5008ms wallclock** with `timeout=1`. Expected ~1s if `cts.CancelAfter(timeout)` actually cancelled the child's `timer.sleep`. Verified: the fixture sleeps 5s; `test.run timeout=1` maps the result to `TestStatus.Timeout` correctly, but the child App keeps running for the full 5 seconds.

Root cause (per coder's hypothesis, which I didn't independently verify): `timer.sleep` awaits `Task.Delay(ms, Context.CancellationToken)` — the `Context.CancellationToken` on the child's context is not rebound to the per-test `cts.Token` that `test.run` creates.

**Impact**: A real runtime cancellation wiring bug. Parallel tests are fine because the semaphore throttles concurrency, but a single test with a 60s sleep inside a `timeout=1` wrapper will burn 60s of wall time, not 1s. CI wall-time budgets break.

**Suggestion**: Route `cts.Token` into the child App's root context before dispatching. Out of scope for test-quality fixes — belongs to the next coder iteration. Logging for coder v4.

## Verdict rationale

Suite green at 19/19 PLang + 2271/2272 C#. The 10 strong tests discriminate (deletion test confirmed). The 9 weak tests pass findings F1–F9 — none hide a real bug; they either add no value (tautologies, duplicates) or test a weaker claim than their name implies. No shipping blocker. The architectural refactor (condition.elseif/else as first-class actions) is functionally correct; only gap is the elseif-matches path (F4).

**Verdict: needs-fixes** — none of the findings block shipping, but F1, F2, F3, F4 are real test-quality issues that deserve a cleanup pass before handing off to security. Specifically:
- F1 (assert 1=1): remove or strengthen
- F2 (isolation pairing): either delete or pair with a polluter
- F3 (3 identical Reports): deduplicate or make distinct
- F4 (no elseif-matches test): add one real .test.goal

F5–F9 are minor nits. F10 is a latent runtime bug for the next coder iteration, not test-quality.
