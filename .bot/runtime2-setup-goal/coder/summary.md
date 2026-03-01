# runtime2-setup-goal / coder

## v1 — Setup.goal Run-Once Execution System
Implemented Setup object using `engine.System.DataSource` for persistence (table `"setup"`, key = step.Hash). Refactored Goal.RunAsync to delegate step iteration to Steps.RunAsync (OBP rule 5). Setup goals excluded from regular lookup. context.Setup propagates through goal.call. C# tests: 1474/1474, PLang tests: 23/23. See [v1/summary.md](v1/summary.md).

## v2 — Code Analyzer Fixes
Fixed 3 findings: (1) failed setup steps no longer permanently recorded — only record on success or tolerated error, (2) Record returns `Task<Data>` instead of swallowing errors, (3) `All`/`Count`/`Value` now exclude setup goals consistent with `Get()`. Added 2 new tests. C# tests: 1476/1476, PLang tests: 23/23. See [v2/summary.md](v2/summary.md).

## v3 — Share SettingsData across all actors
Fixed `%Settings.ApiKey%` silently returning null. SettingsData was only on System actor — now Engine owns a single instance shared across all actors. Tests updated to use User context (what PLang code actually uses). C# tests: 1478/1478. See [v3/summary.md](v3/summary.md).

## v4 — Tester v3 Findings
Record failure aborts setup. Added `IsTolerableError` for runtime1-compatible "already exists"/"duplicate column name" tolerance. Skip test now proves skip via data marker. Cancellation test added. C# tests: 1485/1485. See [v4/summary.md](v4/summary.md).

## v5 — Auditor v4 Findings (IsSetup filter gaps)
Fixed 3 auditor findings: (1) GetAsync/GetByPrPathAsync now filter IsSetup at all disk-load paths, (2) goals loaded before Setup.RunAsync in Executor.Run2, (3) 'setup' goal name only reserved when setup goals exist. Found bonus bug: cached setup goal in GetByPrPathAsync fell through to disk load causing NPE. C# tests: 1490/1490. See [v5/summary.md](v5/summary.md).

## v6 — Scoped Setup Discovery
Replaced eager `LoadFromDirectoryAsync` (loads ALL .pr files) with `Setup.DiscoverAsync` (loads only setup goals). Non-setup goals remain lazy-loaded via `GetAsync`. OBP rule 1: Setup owns its own discovery. C# tests: 1493/1493. See [v6/summary.md](v6/summary.md).
