# Tester v5 — Summary

## What this is

Review of coder v3's F1 discharge — 19 `.test.goal` files got bodies + `condition.elseif`/`condition.else` promoted to first-class actions + `test.tag` returns accumulated tags + enum↔string normalization in `Operator.NormalizeTypes`. The coder claimed 19/19 Pass; my job was to verify that and apply the deletion test to every test body.

## What was done

Ran the full C# and PLang suites, regenerated C# coverage via coverlet on the four changed namespaces, read every `.test.goal` file and its `.pr`, applied the deletion test, spot-checked where the new C# code is or isn't covered. Wrote findings to `v5/result.md` and structured report to `.bot/runtime2-test-module/test-report.json`.

**Result: needs-fixes** — suite is green but 4 of 19 PLang tests are weak (tautology, no cross-test pollution source, triplicated identical Reports, missing elseif-matches path). No shipping blocker; these are test-quality cleanups.

### Test results

- **C#**: 2271/2272 pass. Same pre-existing LLM flake as v4 baseline.
- **PLang**: 19/19 pass *after rebuilding* PlangConsole. The installed binary was stale (16:09 pre-dated the 21:20 source); initial run showed 14/19 as the runtime didn't know about `condition.elseif`/`condition.else` or the enum normalization. After `dotnet build PlangConsole`, all 19 pass.

### Coverage on coder-touched code (C# unit tests only)

- `test/run.cs`, `test/tag.cs`, `condition/if.cs`, `condition/Operator.cs`, `DefaultEvaluator.cs`, `Actions/Action/this.cs`, `Actions/this.cs` — all 71–100%.
- **`condition/elseif.cs`**: 0% line / 0% branch from C# tests. Covered end-to-end by PLang `TestConditionIfRecordsBranchIndexElseBranch`, but 0% from coverlet's view.
- **`condition/else.cs`**: 0% line / 100% branch (trivial `Data(true)`). Same end-to-end coverage note.

### Findings (10 total)

**Major (4):**
1. **F1** — `TestRunSubscribesAfterActionForCoverage` ends with `assert 1 equals 1`. Claims to guard coverage subscriber wiring but observes nothing.
2. **F2** — `TestRunIsolatesMemoryStackBetweenTests` asserts `%isolation_probe%` null at entry, but no other test writes `isolation_probe`. No source of pollution to leak in.
3. **F3** — 3 Report tests (`WritesJunitXml`, `IncludesCoverageTables`, `RendersFailureWithVariables`) have IDENTICAL bodies — all just `read file /system/test.goal, assert contains 'test.report'`. Three tests for one string.
4. **F4** — No test covers the elseif-matches path (b=1) with the new `condition.elseif` action. Only b=0 (True) and b=2 (Else) are covered end-to-end. Existing C# tests use the OLD model (two `condition.if`).

**Minor (5):**
5. `TestAssertFailureSnapshotsVariables` stops at Status=Fail, doesn't reach into `AssertionError.Variables`. Coder self-flagged.
6. `TestTagOutsideTestIsNoOp` tests accumulate branch, not no-op. Name misleads. Coder self-flagged.
7. `OrchestrateBranchCoverageTests` subscriber mirrors OLD filter (`IsCondition && IsFirstConditionInStep`), production now uses `IsIfHead`.
8. No C# unit test for `Operator.NormalizeTypes` enum↔string branch. Covered end-to-end only.
9. `TestSystemTestGoal{NoForeach,RunsAllDiscovered}` overlap — only the foreach-absence differs.

**Latent runtime bug (1, coder self-flagged, out of tester scope):**
10. `test.run timeout=1` maps `TestStatus.Timeout` correctly but doesn't cancel the child's `timer.sleep`. `TestRunEnforcesTimeout.test.goal` takes 5008ms wallclock; expected ~1000ms. cts.Token passed to `RunGoalAsync` but downstream handlers read the child's unbound `Context.CancellationToken`.

## Code example — the F1 finding shape

Before (v3 — the tautology):
```
TestRunSubscribesAfterActionForCoverage
/ Executes file.read and output.write so the test.run AfterAction subscriber
/ records them in coverage.
- read file '/system/test.goal', write to %goalText%
- write out 'coverage probe'
- assert 1 equals 1
```

The claim ("observable regression guard") is vapor — nothing observes the coverage table. Suggested fix:
```
- read file '/system/test.goal', write to %goalText%
- write out 'coverage probe'
- assert %coverage.module_actions% contains 'file.read'   / if PLang can introspect Testing.Coverage
```

If PLang can't introspect `Testing.Coverage` from a `.test.goal`, either add a `test.coverage` primitive or downgrade the comment to admit this is a coverage-exercise probe, not a guard.

## What I would do differently on a v6

- Verify the stale-binary dance up front with `ls -la` on the binary and source before running the PLang suite. Wasted 5 minutes chasing false failures before noticing the timestamp mismatch.
- Explicitly call out that C# coverage via coverlet misses PLang-runtime-only test execution. The "elseif.cs 0%" number looks alarming but the end-to-end path does exercise it.

## Handoff

**Next: coder v4** to address F1–F4 (the majors). F5–F9 are minor nits the coder can pick up opportunistically. F10 is a latent runtime bug for a follow-up iteration — route `cts.Token` into the child App's context.

After coder v4 addresses the majors, re-route to tester v6 for verification. Then security.
