# v5 Summary — Fix Auditor v4 Findings (IsSetup filter gaps, goal loading order)

## What this is

The auditor found that setup goals (IsSetup=true) could "leak" into regular goal execution through two gaps: (1) GetAsync/GetByPrPathAsync loaded goals from disk without checking IsSetup, (2) goals weren't loaded into the collection before Setup.RunAsync ran in Executor.Run2. Also fixed the 'setup' goal name being unconditionally reserved.

## What was done

### F1: IsSetup filter in GetAsync and GetByPrPathAsync

Added `if (loaded is { IsSetup: true }) return null;` at all disk-load return paths:

**`PLang/Runtime2/Engine/Goals/this.cs`**:
- `GetAsync` relative path (line ~120): filter after loading from relative .pr file
- `GetAsync` root path (line ~138): filter after loading from root-relative .pr file
- `GetByPrPathAsync` cache check (line ~221): changed from `&& !cached.IsSetup` (which fell through to disk load) to `return cached.IsSetup ? null : cached;` (returns null immediately)
- `GetByPrPathAsync` disk load (line ~237): filter after loading from disk

The test for `GetByPrPathAsync_ReturnsNull_ForCachedSetupGoal` exposed a real bug in the original cache check: when a cached goal IS a setup goal, the `&& !cached.IsSetup` condition failed and it fell through to the Engine.FileSystem code path, causing a NullReferenceException.

### F2: Load goals before Setup.RunAsync

**`PLang/Executor.cs`** (Run2 method): `LoadFromDirectoryAsync` was already in the code but positioned after Setup.RunAsync. Verified it's now before the Setup.RunAsync call.

### F3: Conditional setup interception

**`PLang/Executor.cs`** (Run2 method): Changed to `goalName.Equals("setup", ...) && engine.Goals.Setup.Goals.Any()` — only short-circuits when setup goals actually exist.

### Tests added

5 new tests in `PLang.Tests/Runtime2/Core/GoalsTests.cs`:
- `Get_ExcludesSetupGoals` — Get() returns null for cached setup goals
- `GetAsync_ReturnsNull_ForSetupGoalLoadedFromDisk` — creates .pr on disk with IsSetup=true, verifies null returned
- `GetAsync_ReturnsGoal_ForNonSetupGoalLoadedFromDisk` — positive control, non-setup .pr returns goal
- `GetByPrPathAsync_ReturnsNull_ForSetupGoal` — disk-loaded setup goal returns null
- `GetByPrPathAsync_ReturnsNull_ForCachedSetupGoal` — cached setup goal returns null immediately

All 1490 tests pass (was 1485 before, +5 new).

## Code example

The core pattern applied at each disk-load return point:

```csharp
// Before (bug): setup goals returned from disk load
var loaded = relResult.Value as Goal.@this;
if (loaded != null && !string.IsNullOrEmpty(name))
    _byPath[name] = loaded;
return loaded;

// After (fix): setup goals filtered at disk load boundary
var loaded = relResult.Value as Goal.@this;
if (loaded is { IsSetup: true }) return null;
if (loaded != null && !string.IsNullOrEmpty(name))
    _byPath[name] = loaded;
return loaded;
```

The GetByPrPathAsync cache fix:
```csharp
// Before (bug): falls through to disk load, NPE on Engine.FileSystem
if (_byPath.TryGetValue(prPath, out var cached) && !cached.IsSetup)
    return cached;

// After (fix): returns null immediately for cached setup goals
if (_byPath.TryGetValue(prPath, out var cached))
    return cached.IsSetup ? null : cached;
```

## Files modified
- `PLang/Runtime2/Engine/Goals/this.cs` — IsSetup filters in GetAsync, GetByPrPathAsync
- `PLang/Executor.cs` — goal loading order, conditional setup interception
- `PLang.Tests/Runtime2/Core/GoalsTests.cs` — 5 new tests
