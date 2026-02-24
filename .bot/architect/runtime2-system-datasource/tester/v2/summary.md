# Tester v2 Summary — DataSource + Settings Bridge

## What this is
Test quality analysis of the DataSource persistence layer and Settings variable bridge feature (coder v2, post-codeanalyzer v2 PASS).

## Test Run Results
- **C# tests**: 1460 passed, 0 failed
- **PLang tests**: 22 passed, 0 failed (but see critical findings below)
- **Coverage**: dotnet-coverage profiler failed to initialize; manual analysis performed

## Critical Findings

### F1: PLang Test Runner Cannot Detect Assertion Failures (Pre-existing Bug)
The test runner's assertion tracking mechanism is fundamentally broken:
1. `TrackAssertionFailures` is registered on the `AfterStep` event
2. But `Step.RunAsync` (line 74) returns immediately on ANY failure, **skipping the AfterStep event entirely**
3. The test runner's fallback (line 146) explicitly excludes `AssertionError` from error detection
4. Result: `__stepResult` is never stored in MemoryStack, and the AfterStep event never fires for failed steps

**Proof**: Running `Settings/SetMaxGzipSize/Start.test.goal` manually produces `Error(400) — AssertionFailed: Expected: 20971520, Actual: (null)`. But the test runner reports PASS.

**Impact**: ALL 22 PLang tests could have failing assertions and we wouldn't know. This is a systemic false-green factory.

### F2: Settings PLang Test is a False Green
`Tests/Runtime2/Settings/SetMaxGzipSize/Start.test.goal` has a `.pr` file that maps to:
- Step 0: `variable.set` (not `settings.set`)
- Step 1: `variable.get` (not `settings.get`)
- Variable names don't match between steps: sets `max gzip size`, gets `archive.max`

This test provides **zero coverage** of the settings module.

### F3: No PLang Tests for Settings Module
The `settings.set`, `settings.get`, and `settings.remove` action handlers have no PLang integration test. The builder → .pr → GoalMapper → runtime pipeline is untested for settings.

## Major Findings

### F4: SettingsData DataSource Error Path Untested
`SettingsData.GetChild` lines 54-55 (`if (!result.Success) return result;`) — when the DataSource returns an error (database locked, disk error), this path executes. No test covers it.

## Minor Findings
- F5: MemoryStack.Clone shares SettingsData by reference; Context setter mutates original's SettingsData.Context
- F6: DeserializeValue doesn't catch InvalidOperationException from depth overflow
- F7: GetAll with empty table not tested
- F8: Test cleanup uses bare catch

## Verdict: FAIL (needs-fixes)

The C# test quality is good — coder v2 addressed all codeanalyzer findings properly. But:
1. The test runner bug means PLang test results are unreliable (any failing assertion is silently ignored)
2. The Settings PLang test is a false green
3. No real PLang integration tests exist for the new settings module

The test runner bug (F1) is pre-existing (not introduced by this branch), but it directly impacts our ability to validate this feature. F2 and F3 are this-branch issues.

## Files Modified
- `.bot/architect/runtime2-system-datasource/test-report.json` (findings)
- `.bot/architect/runtime2-system-datasource/tester/v2/verdict.json` (fail)
- `.bot/architect/runtime2-system-datasource/tester/v2/summary.md` (this file)
- `.bot/architect/runtime2-system-datasource/tester/v2/plan.md`
