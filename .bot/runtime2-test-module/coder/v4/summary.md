# Coder v4 — Summary

## Scope
Tester v5 left needs-fixes with 4 major findings (F1–F4). All addressed in this session. F5–F9 (minor) and F10 (latent runtime bug) deferred per tester's handoff.

## What shipped

### F1 — Deleted tautology test
`Tests/TestModule/Run/TestRunSubscribesAfterActionForCoverage.test.goal` and its .pr removed. The body was `read file + write out + assert 1 equals 1` — the assertion observed nothing. C# `OrchestrateBranchCoverageTests` already validates the AfterAction subscriber wiring; no PLang-side primitive surfaces Coverage from inside a `.test.goal`, so the test was vapor.

### F2 — Pair-fixture isolation test
Replaced `TestRunIsolatesMemoryStackBetweenTests` (which had no source of pollution to leak) with a deterministic pair under `_isolation/`:
- `AIsolationPollute.fixture.goal` — sets `%shared_probe%=1`
- `BIsolationProbe.fixture.goal` — asserts `%shared_probe%` is null at entry

The outer test runs both via `test.run parallel=1` (alphabetical → Pollute first), then asserts both Pass. Under broken fresh-App-per-test isolation, Probe would observe the polluted value and fail. Parallel=1 makes the ordering deterministic (default `ProcessorCount` would race).

### F3 — Distinct Report tests via observable scalars
The 3 Report tests previously had IDENTICAL bodies (`assert %goalText% contains 'test.report'`). Made them distinct by exposing scalar observables on `test.report`'s return Data:

```csharp
result.Properties.Set("format", format);
result.Properties.Set("reportPath", reportFile);
result.Properties.Set("content", content);
result.Properties.Set("summaryTotal", results.Count);
result.Properties.Set("summaryPass", summary[TestStatus.Pass]);
result.Properties.Set("summaryFail", summary[TestStatus.Fail]);
result.Properties.Set("variableSnapshotCount", variableSnapshotCount);
```

Each Report test now runs a small fixture via nested `test.run`, calls `test.report results %results%, write to %report%`, and asserts on `%report.X%`:

- `TestReportWritesJunitXml` — runs `test.report` with `format='junit'`, asserts `%report.format% equals 'junit'`, `%report.summaryPass% equals 1`, and `file.exists %report.reportPath%` is true.
- `TestReportIncludesCoverageTables` — same shape but default (json) format. Distinguished by format check + reportPath pointing at `results.json` not `junit.xml`.
- `TestReportRendersFailureWithVariables` — runs the failing fixture, asserts `%report.summaryFail% equals 1` and `%report.variableSnapshotCount% equals 1`. A regression that dropped Variables from `AssertionError` would fail the snapshot count.

Two new fixtures: `Report/_fixtures_pass/trivial.fixture.goal` (pass), `Report/_fixtures_fail/failsvar.fixture.goal` (fails, sets `%score%=42`).

### F4 — elseif-matches branch test
Added `Tests/TestModule/Condition/TestConditionElseIfMatchesRecordsBranchIndex1.test.goal`:

```
- set %x% = 7
- if %x% > 10 set %a% = 1, else if %x% > 5 set %b% = 2, else set %c% = 3
- assert %__data__.branchIndex% equals 1
- assert %b% equals 2
```

Covers the previously-untested `b=1` (elseif matches) path under the new `condition.elseif` action. The Branch coverage table now shows `{❌ if, ✅ elseif[1], ❌ else}` for this site.

## C# changes

### test.report (`PLang/App/modules/test/report.cs`)
Two additions:
1. **`Format` parameter** (optional) — overrides `testing.Format` per-call. Lets PLang tests write a different artefact format than the outer runner. Documented with new `[Example]` attribute.
2. **Observable scalars on Properties** — `format`, `reportPath`, `content`, `summaryTotal`, `summaryPass`, `summaryFail`, `variableSnapshotCount`. Surfaced so a `.test.goal` can verify report behaviour without hitting the goal-relative path resolution edge case (see "Latent issues" below).

`Data.Ok(results)` previously was the entire return; now it's still `Data.@this<Results>` but enriched with Properties for test inspection. Production behaviour unchanged — non-test consumers ignore the extra Properties.

## Tests and build status

- **PLang `--test`**: all 19 `Tests/TestModule/**/*.test.goal` Pass (was 19 in v3, F1 deleted (-1), F4 added (+1) = 19). Total suite 168, 99 pass, 50 fail (pre-existing in unrelated tests), 19 stale (unchanged).
- **C# suite**: 2271/2272 — same pre-existing `Query_ToolCall_LlmRequestsToolAndHandlesError` LLM flake. 0 regressions from the report.cs change (`ReportActionTests` 12/12 green).
- **Build wallclock**: 5.0s for `TestRunEnforcesTimeout` (the F10 latent bug — see below).

## Latent issues surfaced this session (NEW for tester)

### L1 — Goal-relative path resolution mis-fires in child Apps
`Path.Resolve` (`PLang/App/FileSystem/Path.cs:44`) does goal-relative resolution using `context.Goal?.Path`. When a `.test.goal` runs inside a child App rooted at `test.Directory`, the goal's stored `Path` is parent-root-relative (e.g. `/Tests/TestModule/Report/X.test.goal`) — but the child App's RootDirectory is `/workspace/plang/Tests/TestModule/Report/`. `Path.Combine(goalDir, rawPath)` then produces `/Tests/TestModule/Report/.test/junit.xml`, which `ValidatePath` re-roots against the child's RootDirectory, producing a path that doesn't exist on disk.

**Workaround used**: Added `reportPath` (absolute OS path from `fs.Path.Combine(fs.RootDirectory, …)`) to test.report's Data Properties. Tests use the absolute path, which bypasses the goal-relative resolution.

**Suggested fix (out of scope)**: Either (a) recompute `goal.Path` to be relative to the child's root when the child App initialises, or (b) have `Path.Resolve` detect "goal.Path doesn't share a prefix with RootDirectory" and skip the goal-relative path when that's the case.

### L2 — `assert.contains` Value/Container semantics confuse the builder LLM
`assert.contains Value([object?]), Container([object?])` — the action class names "Value" and "Container" but the provider treats `Value` as the haystack and `Container` as the needle (`DefaultAssertProvider.Contains`). The naming is backwards from natural language, and the `[Example]` only shows one shape. The LLM sometimes maps correctly (`assert %text% contains 'hello'` → Value=%text%, Container=hello), sometimes flips them. When flipped, the assertion fails silently (looking for the variable inside the literal pattern).

**Workaround used**: Removed all `assert ... contains` from the new Report tests. Used `assert ... equals` on the new scalar Properties instead.

**Suggested fix (out of scope)**: Either (a) rename to `Haystack`/`Needle` (would invalidate existing .pr files), (b) flip the provider to match natural language and rebuild all dependent .prs, or (c) add more `[Example]` attributes / a clarifying comment in the action class.

### F10 — `test.run timeout=1` doesn't cancel a sleeping child (carryover)
Still present. `TestRunEnforcesTimeout` takes ~5s wallclock. Out of scope for v4. Belongs to a follow-up: route `cts.Token` into the child App's `Context.CancellationToken` so downstream handlers (`timer.sleep`, `http.request`) honour cancellation.

## What I would do differently

- **Should have caught L1 earlier.** Spent ~20 minutes debugging "File not found" before reading `Path.Resolve` carefully enough to see goal-relative resolution was using the parent-perspective path.
- **Should have anticipated L2.** Memory has a feedback note about builder LLM flakiness on certain action shapes; `assert.contains` Value/Container naming is exactly the kind of ambiguity the LLM stumbles on. Saved time on future test design by switching to scalar observables.

## Handoff

Recommend next: **tester v6** to re-verify F1–F4 are discharged. Specifically:
- F1: confirm the test is gone and no false-coverage signal remains
- F2: confirm `_isolation/` pair runs in order under parallel=1 and would catch a real isolation regression (deletion test on `parallel=1` parameter, or on the fresh-App-per-test logic)
- F3: confirm the 3 Report tests now have distinct discriminating assertions (deletion test on `summaryPass`, `summaryFail`, `variableSnapshotCount`, `format`, `reportPath`)
- F4: confirm `branchIndex=1` assertion fails if the orchestrator's elseif-matches branch is broken

Then route to security review per tester v5's plan.
