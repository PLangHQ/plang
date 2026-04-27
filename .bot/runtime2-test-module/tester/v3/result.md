# Tester v3 — Detailed Findings

## Summary table

| #  | Severity | Type              | Site                                                                        |
|----|----------|-------------------|-----------------------------------------------------------------------------|
| 1  | critical | missing-plang-test | Tests/TestModule/**/*.goal — 19 stubs, wrong extension                    |
| 2  | critical | missing-coverage  | run.cs L80-109 + IsFirstConditionInStep — three-bug cluster unguarded       |
| 3  | critical | missing-coverage  | Coverage.RecordBranchLabel/Chain + Merge union paths 0%                     |
| 4  | critical | missing-coverage  | Executor.Run — 0% coverage on CLI argv parsing                              |
| 5  | major    | false-green       | RunActionTests.Run_ParallelExecution — no concurrency probe                 |
| 6  | major    | false-green       | RunActionTests.Run_SystemDirectory_Inherited — tautology                    |
| 7  | major    | false-green       | RunActionTests.Run_TestingIsEnabled — status-only assertion                 |
| 8  | major    | missing-plang-test | No .test.goal for condition.if branchIndex                                 |
| 9  | major    | missing-coverage  | report.cs JUnit Fail/Timeout/Stale/Skipped XML — 0%                         |
| 10 | major    | weak-assertion    | ConditionIfBranchIndex eval-error conditional assertion                     |
| 11 | major    | weak-assertion    | RunActionTests assertion-failure test misses AssertionError.Variables       |
| 12 | minor    | weak-assertion    | ReportActionTests branch table — Contains("0") / Contains("1")              |
| 13 | minor    | missing-coverage  | Include/Exclude.Clear semantic not tested                                   |
| 14 | minor    | weak-assertion    | Stale/Skipped pass-through — no side-effect probe                           |
| 15 | minor    | missing-coverage  | Apply unknown-key forward-compat not tested                                 |
| 16 | minor    | missing-coverage  | Debug module at 2.1% — widened lambda untested                              |
| 17 | major    | false-green       | AfterAction modifier test — .Any() without count/order assertion            |

## Critical findings (detail)

### 1. PLang test stubs — entire .goal contract unvalidated

`Tests/TestModule/` contains 19 `.goal` files planned by the test-designer, each containing only:

```
TestName
/ comment describing the planned test
- throw "not implemented"
```

Problems:
- Extension is `.goal`, not `.test.goal` → `test.discover` pattern `*.test.goal` does not match them.
- No `.build/` directory exists for any of them → they were never built, never run.
- All 19 would fail immediately via `throw` even if discovered.

The C# test harness exercises each module directly (AfterActionPayloadTests, DiscoverActionTests, etc.) but the PLang bridge — the LLM-produced .pr files, parameter mapping conventions, and system/test.goal wiring — has NO automated test. Per CLAUDE.md: "PLang .goal tests are REQUIRED alongside C# tests — they validate the FULL pipeline: LLM builder → .pr generation → GoalMapper → runtime."

**Fix:** Rename to `.test.goal`, implement the bodies, build, and run. Minimum-viable subset:

- `TestSystemTestGoalNoForeach.test.goal` — builds, checks the .pr of system/test.goal contains no `loop.foreach` action (prevents regression).
- `TestRunIsolatesMemoryStackBetweenTests.test.goal` — meta-test: the runner running two fixture tests where TestB asserts TestA's variable is absent.
- `TestDiscoverHandlesIcelandicGoalNames.test.goal` — multilingual path sanity.

### 2. Three-bug cluster from d05c138d is unguarded

The coder's fix in commit `d05c138d` (`SplitAtConditions` reading via `this[i]` indexer, plus `IsFirstConditionInStep` fallback `?? true` → `?? false`) resolves three bugs that all share the same root cause (`action.Step == null` for inner elseifs in multi-action orchestrate steps):

1. Coverage subscriber recording at phantom site `"?:?"`.
2. `alreadyOrchestrating` guard-key mismatch for inner branches.
3. `DisableChildrenOf` silently skipped for inner elseifs.

Codeanalyzer v3 already called this out: "No existing test catches any of the three bugs." I confirmed this by:

- `grep IsFirstConditionInStep PLang.Tests/` — zero matches.
- Coverage of run.cs:80-109 (the branch-recording block inside the coverage subscriber) — 0% line coverage.
- `MultiBranch_SecondBranchMatches_BranchIndexIs1` captures only the outer's AfterAction (via `ReferenceEquals(action, first)`), so the inner's phantom AfterAction firings are invisible to the test.

**Fix:** Exactly the test codeanalyzer described:

```csharp
[Test]
public async Task MultiActionOrchestrate_InnerElseIfMatches_FilterSkipsPhantomSites_SubStepsRun()
{
    // Build step with 4 actions: [if outer false, body A, elseif inner true, body B]
    // Plus two indented sub-steps that should ONLY run when some branch matches.
    //
    // Attach the PRODUCTION coverage subscriber — same shape as run.cs:78-115,
    // including filter `action.IsCondition && action.IsFirstConditionInStep`.
    //
    // Assertions:
    //   - coverage.Branches has exactly one entry, keyed to outer goal:step
    //   - coverage.Branches[...].Contains(1) — the inner branch index
    //   - no entry keyed "?:?"
    //   - the indented sub-steps were executed (track via side-effect variable)
}
```

Belt-and-suspenders: also assert `action.IsFirstConditionInStep == false` on the inner action directly.

### 3. RecordBranchLabel / RecordBranchChain / Merge — 0% coverage

`Coverage.cs:59-63` (`RecordBranchLabel`), `:81-85` (`RecordBranchChain`), `:104-109` (`Merge` label union), `:111` (`Merge` chain union) are never hit by any test.

These are features shipped by commits `bc18d1b6` and `8f7fcaf6` as part of the "human-readable labels + ✅/❌ per branch" cleanup. Production condition.if writes `branchChain` via `Properties` and run.cs forwards it into `Coverage.RecordBranchChain` — but nothing tests the recording or the merge.

Without these, `CoverageTests` covers only the plain index-based recording/merge; the label+chain-based path is invisible. If `Merge` silently drops labels on collision, or `RecordBranchLabel` over-writes instead of accumulating, nobody finds out.

**Fix:** Five new CoverageTests:

- `RecordBranchLabel_Site_AccumulatesLabels`
- `RecordBranchLabel_SameLabel_Idempotent`
- `RecordBranchChain_FirstWins_SubsequentIgnored`
- `Merge_UnionsBranchLabels`
- `Merge_UnionsBranchChains_FirstWins`

### 4. Executor.Run — 0% coverage on CLI argv parsing

`PLang/Executor.cs` `Run(string[] args)` L17-87 has 0% line coverage. Every test goes through `App.@this` directly or pokes `Testing.Apply` via a dict. Nothing exercises the `string[] args → CommandLineParser → engine config` path.

Specifically uncovered:
- `--test` presence check + `Testing.IsEnabled = true` + routing to `system/.build/test.pr` (L42-53, L75-78)
- `--debug` value passed to `engine.Debug.Apply` (L38-39)
- `--build` routing (L60-71)
- `--app` routing (L56-57)
- Variable injection from CLI parameters (L31-35)

**Fix:** Add `ExecutorTests.cs`:

- `Run_TestFlag_SetsTestingIsEnabledAndRoutesToSystemTestPr`
- `Run_TestFlagWithDict_AppliesConfigToTesting`
- `Run_TestFlagWithInvalidConfig_ReturnsApplyError`
- `Run_DebugFlag_InvokesDebugApply`
- `Run_CliParameters_InjectedIntoUserVariables`
- `Run_NoSpecialFlags_StartsTargetGoalFile`

## Major findings (detail)

### 5-7. Three tautology tests in RunActionTests.cs

Common pattern: the test body asserts only what the test runner's mere existence guarantees.

**Test 5 (`Run_ParallelExecution_RespectsSemaphoreLimit`):** four tests execute with `parallel: 2`, assertion is `runs.All(r => r.Status == TestStatus.Pass)`. This passes with parallel=1 (serial), parallel=2 (intended), or parallel=100 (unbounded). The semaphore logic is unexercised.

**Test 6 (`Run_SystemDirectory_InheritedFromParentApp`):** sets `_app.SystemDirectory = "/some/system/dir"`, runs a fixture, asserts `_app.SystemDirectory == "/some/system/dir"` at the end. This is `x = y; assert x == y` — always true. The test comment admits this.

**Test 7 (`Run_TestingIsEnabled_SetToTrueInChildApp`):** runs a fixture, asserts `Status == Pass`. The status is decided by whether the fixture's `variable.set` step succeeds, which has nothing to do with `Testing.IsEnabled` on the child App.

All three need a **probe mechanism** inside the child App. Options:
- Register a BeforeAction binding on the child that snapshots the observable (SystemDirectory, IsEnabled, concurrency counter) into a shared reference external to the child.
- Construct fixtures that are deliberately sensitive to the behaviour — e.g. a fixture that calls a sub-goal reachable only via the parent's SystemDirectory, so the test fails at resolution if SystemDirectory wasn't inherited.

### 9. JUnit XML — only Pass case is tested

`report.cs:272-288` is the `switch (run.Status)` block with Fail/Timeout/Stale/Skipped cases. All four have 0% coverage. Only `Report_Format_Junit_WritesJunitXml` exists and it writes a single Pass test.

CI systems parse JUnit XML and surface <failure>, <skipped> elements in dashboards. A malformed XML here cascades into CI pipeline breakage. Add four tests mirroring `Report_JUnit_TestNameWithXmlSpecialChars_Escaped`: parse the output and assert the element structure.

### 10. Weak conditional assertion

```csharp
// ConditionIfBranchIndexTests.Evaluation_ThrowsOrErrors_NoBranchIndexPublished
var result = await RunSingleStep(action);
await Assert.That(result).IsNotNull();
if (!result.Success)
    await Assert.That(result.Properties.Contains("branchIndex")).IsFalse();
```

If `result.Success` is `true` (maybe `Operator(">")` works on an `object` after coercion), the inner assertion is silently skipped — the test is green without proving anything. Strengthen by first asserting `result.Success == false`.

### 11. AssertionError.Variables not verified end-to-end

`Run_AssertionFailureInTest_CapturedInResult_NoPropagatedException` verifies `run.Error is AssertionError` but never checks `((AssertionError)run.Error).Variables != null`. Yet the whole point of the AssertSnapshot wrapper is so that Variables flow all the way from the assert handler → provider → test.run's failure path → TestRun. No test covers this end-to-end chain.

## Verdict

**needs-fixes.**

Specifically: the suite passes (2244 tests, 2243 pass, 1 pre-existing flake), but multiple tests are false greens or have 0% coverage on the critical new code paths. The fix in `d05c138d` — which codeanalyzer v3 blessed — is correct but has no guard test. A future refactor that reintroduces `_items[i]` or flips the `?? false` fallback can ship without any test catching it.

### Next steps

- Coder: propose/write test for three-bug cluster (finding #2).
- Coder: replace 19 Tests/TestModule/**/*.goal stubs with real tests, rename to .test.goal (finding #1).
- Coder: tighten the three tautology tests in RunActionTests (findings #5-7).
- Coder: add JUnit non-Pass cases (finding #9), AssertionError.Variables integration (finding #11).
- Coder: add RecordBranchLabel/Chain/Merge tests (finding #3).
- Coder: add Executor.Run CLI parsing tests (finding #4).
- Tester: re-verify after fixes.

After fixes, this is a strong suite and a strong implementation. The implementation quality is high — the fix is surgical, the types are right, the mostly-thorough C# tests catch a lot. The weakness is concentrated in tests whose authors knew they were taking a shortcut (the three tautology tests have explicit "we can't easily probe" comments) and in the gap between the C# test layer and the PLang .goal layer.
