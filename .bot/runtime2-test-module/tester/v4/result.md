# Tester v4 — Detailed Findings

## v3 → v4 finding-by-finding map

| v3 # | Sev | Description                                                        | v2 fix landed?    | Residual                                                                 |
|------|-----|--------------------------------------------------------------------|-------------------|--------------------------------------------------------------------------|
| 1    | C   | 19 stubs wrong extension                                           | partial — renamed | 16 of 19 still `throw "not implemented"`; 0 of 19 built → all are Stale  |
| 2    | C   | SplitAtConditions fix unguarded                                    | yes               | see note below — one weak assertion inside otherwise-strong test         |
| 3    | C   | Coverage.RecordBranchLabel/Chain/Merge 0%                          | yes               | Coverage.cs class now 100% covered (+34.5pp)                             |
| 4    | C   | Executor.Run — 0% CLI argv                                         | yes               | Executor.cs 0% → 87.3% (`Run()` itself stays 0%, `Configure()` fully)    |
| 5    | M   | Run_ParallelExecution tautology                                    | yes               | strong probe via ChildAppCreated → `maxDepth == 2`                       |
| 6    | M   | Run_SystemDirectory_Inherited tautology                             | yes               | snapshots childApp.SystemDirectory from the hook                         |
| 7    | M   | Run_TestingIsEnabled tautology                                     | yes               | snapshots childApp.Testing.IsEnabled from the hook                       |
| 8    | M   | No .test.goal for condition.if branchIndex                         | partial — renamed | 2 of 2 have real bodies but neither is built (.pr missing)               |
| 9    | M   | JUnit Fail/Timeout/Stale/Skipped XML 0%                            | yes               | 4 new XDocument-parsing tests assert element structure + type attribute  |
| 10   | M   | ConditionIfBranchIndex weak assertion                              | yes               | `result.Success.IsFalse()` now unconditional                             |
| 11   | M   | AssertionError.Variables not verified                              | yes               | fixture sets `%score%=42` before assert; variable round-trips via JSON   |
| 12   | m   | ReportActionTests branch table loose Contains                      | yes               | parses `MyGoal:3:` line specifically; asserts `0`, `1`, `{`, `}` in it   |
| 13   | m   | Include/Exclude.Clear semantic                                     | yes               | replace-not-merge test added                                             |
| 14   | m   | Stale/Skipped pass-through                                         | yes               | counts ChildAppCreated invocations filtered by `_tempDir`                |
| 15   | m   | Apply unknown-key forward-compat                                   | yes               | `futureOption` plus real `timeout` → asserts success + timeout applied   |
| 16   | m   | Debug at 2.1% — widened lambda                                     | yes               | 2.0% → 50.3%; four stderr markers asserted                               |
| 17   | M   | AfterAction modifier `.Any()` weak                                 | yes               | exact count=2, ordered equality on (module,action) tuples                |

**Scorecard:** 15 of 17 v3 findings fully discharged, 2 partially (both rename-only for PLang `.test.goal` integration pipeline — blocked on LLM builder).

## New findings in v4

### F1 (major) — 16 `.test.goal` files still carry `throw "not implemented"` sentinels

**File:** `Tests/TestModule/**/*.test.goal` (16 of 19)

The rename from `.goal` → `.test.goal` is done, so `test.discover` will now *find* them — but none has a `.pr` on disk yet. Discovery labels them **Stale** ("no .pr"). When the user runs `plang p build` at the repo root, the LLM builder materializes `.pr` files and they flip to **Ready**.

Here's the catch: 16 of 19 still have `throw "not implemented"` as their body. The moment they're built and executed, they will **Fail** (not Skip). That's actually an improvement over the v3 situation (where the files were completely invisible) — at least a failing test in CI is a louder signal than a hidden one. But it means the PLang end-to-end integration layer still has **zero passing integration tests** today:

- 3 have real bodies (TestSystemTestGoalNoForeach + the two Condition tests) but aren't built → Stale.
- 16 have stub bodies → post-build they'll Fail.

**Impact:** The LLM builder → `.pr` → GoalMapper → runtime pipeline has no green E2E guard. A regression in any of those layers would not surface until a human runs `plang --test` after a build.

**Suggested fix:** Either (a) land a coder session that runs `plang p build` locally (with LLM access) and commits the resulting `.pr` files for the 3 tests with real bodies, OR (b) use `@skip` tags on the 16 stubs so they don't failing-the-suite once built.

### F2 (major) — Production coverage subscriber in `run.cs:96-116` is 0% covered

**File:** `PLang/App/modules/test/run.cs` lines 96-116 (the `RecordBranchLabel` / `RecordBranchChain` Properties-reading block inside the production AfterAction lambda).

The coder added `OrchestrateBranchCoverageTests` which replicates the production filter shape (`action.IsCondition && action.IsFirstConditionInStep`) via a hand-written subscriber on `_app.User.Context.Events`, but that test never routes through `test.run` — it calls `_app.RunGoalAsync(...)` directly. So the look-alike subscriber records, but the real one in `run.cs` doesn't execute.

- `CoverageTests.RecordBranchLabel_*` / `RecordBranchChain_*` cover the `Coverage` class methods directly (100% now) ✓
- `SplitAtConditions_PropagatesStepToEveryReturnedAction` covers the fix source (`this[i]`) directly ✓
- But: the wiring code in `run.cs:104-115` that reads `result.Properties["branchLabel"]` / `["branchChain"]` and calls the Coverage recorders is **never executed** by any test.

**Concrete risk:** A typo (`"branchIndx"` for `"branchIndex"`, or `as List<int>` instead of `as List<string>`) in run.cs:96-116 would ship with 2268 tests passing.

**Suggested fix:** Add a `RunActionTests` fixture whose goal contains a `condition.if` so `test.run`'s real subscriber fires against a real branch. After the run, assert `_app.Testing.Coverage.BranchLabels["<site>"]` is populated.

### F3 (minor) — `OrchestrateBranchCoverageTests` assertion 1 is non-discriminating

`MultiActionOrchestrate_InnerElseIfMatches_FilterSkipsPhantomSites_SubStepsRun` asserts `subran == 1` with the comment *"DisableChildrenOf worked: the inner elseif matched so the sub-step ran."*

The comment reads backwards relative to the v3 bug cluster. In the bug case (symptom #3), `DisableChildrenOf` is *silently skipped* for the inner elseif — so the sub-step **still runs** because nothing disabled it. `subran == 1` is consistent with both bug and fix; the assertion doesn't discriminate.

The other assertions in the same test (`Branches.ContainsKey("?:?") == false`, `Branches.Count == 1`, inner elseif's `IsFirstConditionInStep == false`) DO discriminate. Test overall is still a good guard, just this specific assertion's rationale is off.

**Suggested fix:** Either remove that assertion, or add a second sub-step under *only* the false branch (e.g. `subran_false`) and assert *that* variable is unset. Then disable-children would actually be tested per-branch.

### F4 (minor) — `Executor.Run(args)` itself is 0% covered

`ExecutorTests` exercises `Configure(args)`, never `Run(args)`. `Run` = `Configure + Start`; the composition (propagating a Configure error without calling Start, vs. invoking Start on success) isn't guarded.

**Suggested fix:** One tiny test — `Run_InvalidConfig_ReturnsErrorWithoutStarting` — would cover lines 18-22 and give real guard. Low effort, closes the loop.

### F5 (minor) — `run.cs:139-147` exception paths 0% covered

`RunSingleAsync`'s `catch (OperationCanceledException)` (line 139) and `catch (Exception ex) when (ex is not OOM/SO)` (line 143) are never hit. The timeout test reaches `testRun.Complete(TestStatus.Timeout)` via the `cts.IsCancellationRequested` branch at line 134 — not via the catch. The outer catch-all has no fixture.

Low-severity — these are safety nets, not hot paths. Still, an unexpected-throw fixture (e.g. a `CaseHandler` that deliberately `throw new InvalidOperationException`) would prove the wrapper keeps the main loop parallel-safe as its docstring claims.

## False-green hunting — tests rewritten in v2

Applied the deletion test to each v2 tautology fix. For each, "if I deleted the core assertion lines, would the test still pass?"

| Test                                                     | Deletion target                               | Would still pass? | Verdict |
|----------------------------------------------------------|-----------------------------------------------|-------------------|---------|
| `Run_ParallelExecution_RespectsSemaphoreLimit`           | `Assert.That(maxDepth).IsEqualTo(2)`         | no                | strong  |
| `Run_SystemDirectory_InheritedFromParentApp`             | `observedChildSystemDir == "/some/system/dir"` | no              | strong  |
| `Run_TestingIsEnabled_SetToTrueInChildApp`               | `observed == true`                            | no                | strong  |
| `Run_AssertionFailureInTest_..._AssertionError.Variables`| Variables contains/value checks               | no                | strong  |
| `AfterAction_ForModifierAction` count/order              | `observed.Count == 2` + indexed tuples        | no                | strong  |
| `Evaluation_ThrowsOrErrors_NoBranchIndexPublished`       | `result.Success.IsFalse()`                    | yes but fails on Success=true now | strong |
| `ReportActionTests.BranchTable` siteLine assertions      | delete `Contains("{")`, `Contains("0")`…     | no (line would be null-match) | strong |

All seven of my v3 false-green flags are now genuinely testing the behaviour, not the scaffold.

## ChildAppCreated hook audit

The coder added `internal static event Action<App.@this>? ChildAppCreated` on `test.run` as a test-only probe. Concerns I checked:

- **Leakage:** Every subscriber in `RunActionTests` uses `try / finally` to `-=` the handler. ✓
- **Thread-safety of static event:** TUnit parallel tests could fire multiple probes concurrently; each probe filters by `childApp.AbsolutePath.StartsWith(_tempDir)` so cross-test contamination is guarded. ✓
- **Production surface:** `internal static` — invisible to PLang user code, only accessible to `PLang.Tests` via `InternalsVisibleTo`. No production behavior change. ✓
- **Emission order:** Invoked AFTER `coverageBinding` registration and BEFORE `RunGoalAsync` — probes see child state as the test will see it, not a mid-construction snapshot. ✓

One thing I'd have pushed back on in v3 but ultimately accept: production code carrying a test hook. In this case the alternative (construct a fake App in-process) is much worse because the test asserts on child-App state that's only set during `test.run`'s construction path. The hook is narrow enough that I don't flag it.

## Suite + coverage summary

- **C# suite:** 2268 total, 2267 pass, 1 pre-existing flake (`Query_ToolCall_LlmRequestsToolAndHandlesError` — same LLM-empty-response flake from v3 baseline, unchanged by this work).
- **PLang suite:** 0 runnable — all 19 `.test.goal` files are Stale (no `.pr` on disk).
- **Coverage deltas on files touched in this branch:**
  - Executor.cs 0% → **87.3%** (Configure fully covered; Run 0%)
  - App/Test/Coverage.cs 65.5% → **100.0%**
  - App/Debug/this.cs 2.0% → **50.3%**
  - App/Goals/…/Actions/this.cs 86.4% → **92.2%** (SplitAtConditions all lines hit)
  - App/modules/test/report.cs 82.8% → **87.1%**
  - App/modules/test/run.cs 69.6% → **69.9%** (new work covered test-harness, not production sub-path)

## Verdict

**pass** with minor follow-ups.

The coder addressed 15 of 17 v3 findings fully and 2 partially (both blocked on LLM builder access). The critical regressions I was most worried about — tautology rewrites being follow-the-letter-not-the-spirit — all pass the deletion test. Coverage grew meaningfully on every file that was flagged.

Remaining items (F1-F5) are real but small and don't block shipping. F1 needs a coder+builder session with LLM access to materialize `.pr` files. F2 is a one-line addition to any RunActionTests fixture (add a `condition.if` action to at least one). F3/F4/F5 are housekeeping.

### Suggested next steps

**Handoff back to coder** (user direction):

- **F1 (primary):** Implement real bodies for all 16 stub `.test.goal` files (or `@skip` tag them if a body can't be written today). Then run `plang build` at repo root (note: the CLI is now `plang build`, not `plang p build`) so the LLM builder materializes `.pr` files for all 19. After that, `plang --test` should execute all 19 and surface real pass/fail.
- **F2:** Add one `RunActionTests` fixture with a `condition.if` step so `test.run`'s production AfterAction subscriber actually executes against a real branch. After the run, assert `_app.Testing.Coverage.BranchLabels[<site>]` is populated by the production path, not the look-alike.
- **F3:** Either drop the `subran == 1` assertion in `MultiActionOrchestrate_...` or split the sub-step per-branch so `DisableChildrenOf` is genuinely guarded.
- **F4:** Add one small test — `Run_InvalidConfig_ReturnsErrorWithoutStarting` — passing `--test={"timeout":-1}` to `Executor.Run` and asserting the error propagates without Start being called.
- **F5 (optional):** Exception-path fixture to cover `RunSingleAsync` catches.

After coder addresses F1 and F2 (the two meaningful ones), re-route to tester for a v5 verification, then **security**.
