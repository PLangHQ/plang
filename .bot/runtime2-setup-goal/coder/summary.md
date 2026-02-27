# runtime2-setup-goal / coder

## v1 — Setup.goal Run-Once Execution System
Implemented Setup object using `engine.System.DataSource` for persistence (table `"setup"`, key = step.Hash). Refactored Goal.RunAsync to delegate step iteration to Steps.RunAsync (OBP rule 5). Setup goals excluded from regular lookup. context.Setup propagates through goal.call. C# tests: 1474/1474, PLang tests: 23/23. See [v1/summary.md](v1/summary.md).

## v2 — Code Analyzer Fixes
Fixed 3 findings: (1) failed setup steps no longer permanently recorded — only record on success or tolerated error, (2) Record returns `Task<Data>` instead of swallowing errors, (3) `All`/`Count`/`Value` now exclude setup goals consistent with `Get()`. Added 2 new tests. C# tests: 1476/1476, PLang tests: 23/23. See [v2/summary.md](v2/summary.md).

## v3 — Share SettingsData across all actors
Fixed `%Settings.ApiKey%` silently returning null. SettingsData was only on System actor — now Engine owns a single instance shared across all actors. Tests updated to use User context (what PLang code actually uses). C# tests: 1478/1478. See [v3/summary.md](v3/summary.md).
