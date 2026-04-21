# Tester v6 — Plan

Re-verify coder v4 discharge of tester v5's 4 major findings. Minor findings F5-F9 were explicitly out-of-scope for v4; confirm they are still in an acceptable state (no regression). L1, L2, F10 are latent bugs documented by coder — not tester's scope.

## Verification matrix

| Finding | v4 fix | Deletion-test question |
|---|---|---|
| F1 tautology | Test deleted, .pr deleted | Does any other test cover `test.run AfterAction coverage subscriber` outcome? (C# `OrchestrateBranchCoverageTests` — read it and confirm) |
| F2 false-green isolation | Pair `AIsolationPollute` + `BIsolationProbe` under `_isolation/` with `parallel=1` | If I delete the fresh-App-per-test logic in `test.run`, does Probe now fail? If I change `parallel=1` → default, does the ordering still guarantee Pollute runs first? |
| F3 identical Reports | Each Report test asserts distinct scalars on `test.report` Data Properties | If I delete `summaryPass` assignment in report.cs, do the 3 tests fail in different ways? If I swap `format='junit'` → omit, does WritesJunitXml fail? |
| F4 missing elseif-matches | `TestConditionElseIfMatchesRecordsBranchIndex1.test.goal` | If I break `condition.elseif` to set `branchIndex=0`, does this test fail? If I make the elseif body not run, does `assert %b% equals 2` fail? |

## Steps

1. **Read coder v4 output** — done inline during plan.
2. **Run full PLang `--test`** — verify 19/19 (was 19 pre-v4, -1 F1 deleted, +1 F4 added = 19).
3. **Run C# test suite** — verify no regressions from `test.report` Properties additions.
4. **Read each new/changed `.test.pr`** — verify module/action/parameters match the step text (builder non-determinism). Files to read:
   - `Tests/TestModule/Condition/.build/testconditionelseifmatchesrecordsbranchindex1.test.pr` ✓ (already read, LGTM)
   - `Tests/TestModule/Run/.build/testrunisolatesmemorystackbetweentests.test.pr`
   - `Tests/TestModule/Run/_isolation/.build/aisolationpollute.fixture.pr`
   - `Tests/TestModule/Run/_isolation/.build/bisolationprobe.fixture.pr`
   - `Tests/TestModule/Report/.build/testreportwritesjunitxml.test.pr`
   - `Tests/TestModule/Report/.build/testreportincludescoveragetables.test.pr`
   - `Tests/TestModule/Report/.build/testreportrendersfailurewithvariables.test.pr`
   - `Tests/TestModule/Report/_fixtures_pass/.build/trivial.fixture.pr`
   - `Tests/TestModule/Report/_fixtures_fail/.build/failsvar.fixture.pr`
   - `Tests/TestModule/Integration/.build/testsystemtestgoalincludesallthreephases.test.pr`
   - `Tests/TestModule/Integration/.build/testsystemtestgoaldoesnotuseforeach.test.pr`
5. **Apply deletion tests** to F2, F3, F4 fixes:
   - **F2**: Does the pair actually fail if isolation is broken? Requires checking whether `_isolation/*.fixture.goal` pattern is picked up by `test.discover` (it's `.fixture.goal`, not `.test.goal` — is the `pattern` parameter wired?). If `test.discover` silently discovers nothing, the outer assertion `%count% equals 2` would catch it — so that guard is in place. But the real question: does the **actual runner** guarantee fresh App per test, and does the polluter's `%shared_probe%` actually leak in the broken case?
   - **F3**: Review each of the 3 Report tests' assertion surfaces. Confirm they verify DIFFERENT properties. Check a regression in `format` routing would not accidentally still pass the other two tests.
   - **F4**: Re-verify the .pr assigns `module=condition, action=elseif` (not `condition.if` with a flag). Confirm `%__data__.branchIndex%` is actually set by condition.elseif when matching (trace through `elseif.cs`).
6. **Also verify L1 workaround**: does the absolute `reportPath` actually resolve through `file.exists` correctly? The WritesJunitXml test calls `file.exists %report.reportPath%, write to %exists%`. If L1 affects `file.exists` too, the test would false-negative. Check that `file.exists` handles absolute paths.
7. **Check for new test-quality problems introduced by v4**:
   - The `assert 1 equals 1` tautology was removed, but did the v4 rewrite introduce any new weak assertions?
   - The `test.report` now leaks 7 properties to the Data result. Does any production non-test caller trip over them?
   - The new Integration renames (`IncludesAllThreePhases`, `DoesNotUseForeach`) — are they still structural-only tests (source-level grep of `system/test.goal`) rather than behavioral? That was F9's concern. v5 flagged overlap; v4 didn't address F9 but DID rename. Check whether the rename actually improved discriminating power.
8. **Write `test-report.json`** and `verdict.json` and `summary.md`.
9. **Commit + push**.

## Expected outcome

If all 4 major findings discharge cleanly:
- **verdict: approved** → suggest routing to **security** next (per tester v5's handoff).

If any of F1-F4 fixes has a new test-quality gap (e.g., F3 tests accidentally test the same thing via the new scalars):
- **verdict: needs-fixes** → minimal follow-up for coder v5.

F5-F9 remain open but explicitly out of scope. F10 remains open as latent runtime bug.

## Non-goals

- Not reviewing OBP/architecture quality (codeanalyzer's job).
- Not exhaustively re-verifying already-green tests.
- Not fixing L1, L2, F10, F5-F9 in tester session.
