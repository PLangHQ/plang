# v5 Plan — Fix Auditor v4 Findings

## F1: GetAsync/GetByPrPathAsync must filter IsSetup

**Files**: `PLang/App/Engine/Goals/this.cs`

Add `IsSetup` filter at all disk-load return paths in `GetAsync` (two paths: relative resolution and root-relative fallback) and `GetByPrPathAsync` (cache check + disk load). Pattern: `if (loaded is { IsSetup: true }) return null;`

Also fix `GetByPrPathAsync` cache check: when a cached goal IS a setup goal, the current `&& !cached.IsSetup` condition falls through to disk load instead of returning null immediately. Change to `return cached.IsSetup ? null : cached;`.

**Tests**: Add 5 new tests in GoalsTests.cs:
- `Get_ExcludesSetupGoals` — verifies Get() returns null for cached setup goals
- `GetAsync_ReturnsNull_ForSetupGoalLoadedFromDisk` — creates .pr on disk with IsSetup=true
- `GetAsync_ReturnsGoal_ForNonSetupGoalLoadedFromDisk` — positive control
- `GetByPrPathAsync_ReturnsNull_ForSetupGoal` — disk load path
- `GetByPrPathAsync_ReturnsNull_ForCachedSetupGoal` — cached setup goal short-circuits

## F2: Load goals before Setup.RunAsync

**Files**: `PLang/Executor.cs`

Add `await engine.Goals.LoadFromDirectoryAsync(engine, engine.AbsolutePath, "*.pr", cancellationToken: cancellationToken);` in Run2 before `Setup.RunAsync`. This was already present in the code — moved it to before the setup call.

## F3: Conditional setup interception

**Files**: `PLang/Executor.cs`

Change the setup interceptor to only short-circuit when setup goals actually exist:
`if (goalName.Equals("setup", ...) && engine.Goals.Setup.Goals.Any())`

## F4: No action needed

Metadata boxing is a known pattern, no code at risk now.
