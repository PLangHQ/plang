# Baseline — coder v1 (runtime2-callback)

Captured before touching any code on Stage 1.

## C# tests (`dotnet run --project PLang.Tests`)

- total: 2720
- passed: 2623
- failed: 97
- skipped: 0
- duration: ~16s

**All 97 failures are test-designer stubs** — bodies are `await Task.CompletedTask; Assert.Fail("Not implemented");`. They are the work for Stages 1-4 and were intentionally written red. Pre-existing real failures: **0**.

Stage 1 owes ~24 of these (the `[S1]` set per `test-designer/v1/test-plan.md`):

- `SnapshotInterfaceTests` × 2
- `AppSnapshotTests` × 4
- `ProvidersSnapshotTests` × 6
- `VariablesSnapshotTests` × 2
- `ErrorsTrailSnapshotTests` × 2
- `StaticsAndModesSnapshotTests` × 3

= 19. Plus a few that overlap into Stage 2 boundaries (e.g. `App_Restore_DispatchesEachSubtree_ToMatchingThisRestore` covers the foundation, not CallStack). Total Stage 1 expected-green: roughly 19-22. Remainder stays red until Stages 2-4 land.

## PLang tests (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`)

Final summary line: `Test summary: 192 total, 181 pass, 0 fail, 0 timeout, 11 stale, 0 skipped`.

- 11 stale: test-designer's `Tests/Callback/*` stubs whose body is `- throw "not implemented"` — the runner classifies them as stale, not fail.
- The transient `[Fail]` lines on `_fixtures_*/sensitivefail.fixture.goal` and `_fixtures_*/failsvar.fixture.goal` are intentional negative fixtures executed inside other test goals to validate the error path; they show up because of how the runner echoes nested fixtures, not as branch-level failures (final summary reports 0 fail).
- **Real PLang failures: 0.**

Stage 1 doesn't touch any PLang surface so this number is expected to stay at 0 fail / 11 stale through the end of the stage.

## Build

`dotnet build PlangConsole`: 410 warnings, 0 errors. Build is clean.
