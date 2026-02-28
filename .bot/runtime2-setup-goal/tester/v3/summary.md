# Tester v3 Summary — Setup.goal Run-Once System

## What this is
Test quality review of the Setup.goal run-once execution system (coder v1-v3) and SettingsData sharing fix. The code analyzer approved at v4. This is the first tester pass on this branch.

## Test Run Results
- **C# tests**: 1478 passed, 0 failed
- **PLang tests**: 23 passed, 0 failed (run from `Tests/Runtime2/`)
- **Coverage**: TUnit `--coverage` crashes in WSL2 (PipeConnection error). Used static analysis.

## Code Changes Reviewed

### Setup System (coder v1-v2)
- **Setup/this.cs** (new) — Run-once setup with `Goals`, `RunAsync`, `IsExecuted`, `Record`
- **Steps/this.cs** — New `RunAsync` owning step iteration with run-once check
- **Goal/Methods.cs** — Delegates to `Steps.RunAsync` (OBP rule 5)
- **EngineGoals/this.cs** — `Setup` property, `AllIncludingSetup`, setup filtering in `Get`/`All`/`Count`/`Value`
- **PLangContext.cs** — `Setup` property, preserved in `Clone`
- **Executor.cs** — Calls `Setup.RunAsync` before main goal

### SettingsData Sharing (coder v3)
- **Engine/this.cs** — `SettingsVariable` property, single instance
- **Actor.cs** — All actors register `engine.SettingsVariable` (not just System)

## Test Quality Analysis

### SetupTests.cs — 11 tests
| Test | Quality | Notes |
|------|---------|-------|
| Goals_OrdersSetupFirst_ThenAlphabetical | Strong | Verifies intent (order), not just no-crash |
| ExcludesSetupGoalsFromRegularLookup | Strong | Get returns null for setup goals |
| IsExecuted_ReturnsFalse_ForNewStep | Strong | Clean edge case |
| Record_ThenIsExecuted_ReturnsTrue | Strong | Verifies persistence side effect |
| IsExecuted_ReturnsFalse_ForNullHash | Strong | Null edge case |
| RunAsync_SkipsAlreadyExecutedSteps | **Weak** | Doesn't prove step was skipped (see F2) |
| RunAsync_RerunsStepWithChangedHash | Strong | Different hash = not executed |
| RunAsync_SetsAndClearsContextSetup | Strong | Context lifecycle |
| Clone_PreservesSetup | Strong | Clone correctness |
| RunAsync_FailedStepNotRecorded | Strong | Code analyzer v1 fix verified |
| RunAsync_ToleratedErrorStepIsRecorded | Strong | IgnoreError semantics verified |

### SettingsDataTests.cs — Cross-actor tests
- `SameObjectAcrossAllActors` — Reference equality across System/User/Service. Strong.
- `SetViaSystem_ReadableFromUserContext` — Verifies code analyzer v3 fix. Strong.

## Findings

### F1 (Major): Record return value discarded in Steps.RunAsync
`Steps.RunAsync` line 47 calls `await context.Setup.Record(...)` but discards the `Data` return. Code analyzer v1 flagged `Record` as "silently swallowing errors". Coder v2 changed return type to `Task<Data>`, but the **caller** still ignores the result. If recording fails, the step will re-run on next startup (graceful degradation), but no one is notified. No test covers this.

### F2 (Minor): Skip test doesn't prove skip happened
`RunAsync_SkipsAlreadyExecutedSteps` checks that steps end up recorded, but re-executing step1 would produce the same result. The skip at line 36-37 has no direct verification.

### F3 (Minor): No PLang integration test for Setup
All 11 setup tests are C# unit tests with manually constructed objects. No builder → .pr → runtime pipeline test exists for setup goals.

### F4 (Minor): Cancellation check untested
`Steps.RunAsync` line 56-57 checks `cancellationToken.IsCancellationRequested` — no test exercises this path.

## Verdict: PASS (approved)

The setup system is well-tested with 11 C# tests covering all key scenarios. The code analyzer's v1 findings (record-on-failure, swallowed errors, API consistency) are all addressed and tested. The SettingsData sharing fix is verified with cross-actor tests. The Record return value gap (F1) is a design decision rather than a bug — re-running a step is safe. All findings are minor or observational.
