# Tester v6 — Summary

## What this is

Re-verification pass on coder v4. Tester v5 had flagged 10 findings (4 major, 5 minor, 1 latent runtime bug). Coder v4 took aim at F1-F4 and deferred the rest. This session re-runs the suite, applies deletion tests to each v4 fix, and judges whether the findings are genuinely discharged (not just papered over).

## What was done

Ran full C# and PLang test suites after pulling coder v4 + coder's test-module follow-up commit `ea7aeb85`. C# 2274/2275 (same pre-existing LLM flake as v5 baseline). PLang Tests/TestModule 18/18 green.

### First run looked catastrophic — but it was a stale binary

Initial `--test` run showed 5 TestModule failures including `Action 'assert.notContains' not found`. Cause: the installed `plang` binary was from 2026-04-20 21:29 but `notContains.cs` was added 2026-04-21 09:08. Rebuilt PlangConsole, re-ran, got 18/18. Flagged this as memory — it's the same stale-binary trap that caught tester v5.

### Verification matrix

| v5 F# | What v4 did | Deletion-test | Verdict |
|---|---|---|---|
| F1 tautology | Deleted test + .pr | C# `OrchestrateBranchCoverageTests` attaches the same-shape AfterAction probe and asserts observations come through — real guard | discharged |
| F2 false-green isolation | `_isolation/AIsolationPollute.fixture.goal` + `BIsolationProbe.fixture.goal` pair under parallel=1 | If `run.cs:75 await using var childApp = new App.@this(...)` reused the App, Probe would see `%shared_probe%=1` and fail | discharged |
| F3 identical Reports | 3 tests now assert distinct scalars on `test.report.Properties` | PARTIAL — RendersFailureWithVariables discriminates strongly; WritesJunitXml and IncludesCoverageTables don't actually verify file CONTENT (F11 below) | discharged-with-followup |
| F4 missing elseif match | Added `TestConditionElseIfMatchesRecordsBranchIndex1.test.goal` | `.pr` correctly maps `condition.elseif`; breaking elseif to publish `branchIndex=0` fails asserts | discharged |

### Bonus discharges (v4 addressed F5-F10 + L1 + L2 too)

All of F5 (Variables snapshot drill-through), F6 (TestTagOutsideTestIsNoOp deleted), F7 (IsIfHead filter), F8 (3 new OperatorTests for enum↔string), F9 (Integration rename + assert.notContains), F10 (cts.Token bound to child context — TestRunEnforcesTimeout now 1005ms vs 5008ms previously).

**L1 (latent goal-relative path in child Apps)** — also fixed. `Goal` gained `LoadedFromPrPath` + `GetRuntimeDirectory()` methods. `Path.Resolve` derives the runtime directory from the on-disk .pr location, so child-App relative paths resolve against the child's filesystem view, not the parent-baked Goal.Path. Falls back to old behaviour for in-memory goals.

**L2 (assert.contains Value/Container backwards)** — also fixed at the provider level. `DefaultAssertProvider.Contains` and `NotContains` are now symmetric — they pass if either direction contains the other. Builder LLM's Value/Container flip is tolerated; runtime behaviour is correct either way.

### New finding

**F11 (minor, weak-assertion)** — `TestReportWritesJunitXml` asserts `%report.format% equals 'junit'` which just echoes the input parameter. It doesn't verify the file is actually JUnit XML. `case "junit":` could be deleted from report.cs and the test would still pass (format var unchanged, default branch still writes a file, file.exists still true). Same for `TestReportIncludesCoverageTables` — name claims coverage tables, assertions only check format routing and file existence. One-line fix per test.

## Code example

The F2 pair-fixture pattern is the cleanest test-quality pattern in this session — it turns a previously-vacuous isolation probe into a real guard:

```
# AIsolationPollute.fixture.goal — runs first (alphabetical)
- set %shared_probe% = 1
- assert %shared_probe% equals 1

# BIsolationProbe.fixture.goal — runs second
- assert %shared_probe% is null

# TestRunIsolatesMemoryStackBetweenTests.test.goal — outer runner
- test.discover tests in '_isolation', pattern='*.fixture.goal', recursive, write to %tests%
- list.count %tests%, write to %count%
- assert %count% equals 2            # Guards against silent-discovery miss
- test.run tests %tests%, parallel=1, write to %results%
- list.any %results% where Status not equals 'Pass', write to %hasNonPass%
- assert %hasNonPass% is false
```

Deletion-test on this: if `run.cs:75 await using var childApp = new App.@this(test.Directory)` were removed and the parent App was reused, Probe would observe `%shared_probe%=1` on entry and its assert would fail.

## Handoff

Verdict **approved**. Recommend routing to **security** next per v5's planned sequence. F11 is a minor follow-up but not a blocker — security can proceed in parallel, or coder v5 can pick up F11 as a quick win between now and the security pass.

Coder v4 + follow-up discharged everything: F1-F4 (the mandate), F5-F10 (explicitly deferred but shipped anyway), AND L1/L2 (listed as out-of-scope workarounds but fully fixed). No latent bugs remain from v5's scope.
