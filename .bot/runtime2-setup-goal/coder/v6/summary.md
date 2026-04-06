# v6 Summary — Scoped Setup Discovery (replace eager LoadFromDirectoryAsync)

## What this is

The auditor v6 (via security audit) found that `LoadFromDirectoryAsync` in `Executor.Run2` eagerly loads ALL .pr files at startup, violating PLang's lazy-load convention. Only setup goals need discovery before `Setup.RunAsync` — everything else should remain lazy-loaded via `GetAsync`.

## What was done

### Added `DiscoverAsync` to `Setup.@this`

OBP rule 1: behavior belongs to the owner. Setup now owns its own goal discovery.

**`PLang/App/Goals/Setup/this.cs`** — new `DiscoverAsync` method:
- Scans `engine.AbsolutePath` for `*.pr` files recursively
- Parses each via `engine.Channels.ReadAsync<Goal.@this>`
- Only adds goals where `IsSetup == true` to the collection
- Discards non-setup goals (they remain lazy-loadable via `GetAsync`)
- Silently skips unparseable files (they'll fail at lazy-load time)
- Returns `Data.Ok()` or `Data.FromError()` on failure

### Updated `Executor.Run2`

**`PLang/Executor.cs`** — replaced:
```csharp
// Before (eager — loads everything):
await engine.Goals.LoadFromDirectoryAsync(engine, engine.AbsolutePath, "*.pr", cancellationToken);

// After (scoped — setup goals only):
await engine.Goals.Setup.DiscoverAsync(engine, cancellationToken);
```

### Tests added

3 new tests in `PLang.Tests/App/Goals/Setup/SetupTests.cs`:
- `DiscoverAsync_OnlyLoadsSetupGoals` — creates mixed .pr files on disk, verifies only setup goal in collection
- `DiscoverAsync_NonSetupGoalsRemainLazyLoadable` — verifies non-setup goals can still be loaded via `GetAsync`
- `DiscoverAsync_HandlesEmptyDirectory` — no .pr files, succeeds with empty setup

All 1493 tests pass (was 1490, +3 new).

## Code example

```csharp
public async Task<Data> DiscoverAsync(Engine.@this engine, CancellationToken ct = default)
{
    try
    {
        var files = engine.FileSystem.Directory.GetFiles(
            engine.AbsolutePath, "*.pr", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var goal = await engine.Channels.ReadAsync<Goal.@this>(file, ct);
                if (goal == null || !goal.IsSetup) continue;

                foreach (var step in goal.Steps)
                    step.Goal = goal;

                _goals.Add(goal);
            }
            catch
            {
                // Skip unparseable files — they'll fail when lazy-loaded later
            }
        }
        return Data.Ok();
    }
    catch (Exception ex)
    {
        return Data.FromError(Error.FromException(ex));
    }
}
```

## Files modified
- `PLang/App/Goals/Setup/this.cs` — added `DiscoverAsync` method
- `PLang/Executor.cs` — replaced `LoadFromDirectoryAsync` with `Setup.DiscoverAsync`
- `PLang.Tests/App/Goals/Setup/SetupTests.cs` — 3 new tests
