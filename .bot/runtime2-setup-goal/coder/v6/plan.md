# v6 Plan — Scoped Setup Discovery (replace eager LoadFromDirectoryAsync)

## Problem

`Executor.Run2` line 368 calls `LoadFromDirectoryAsync(engine.AbsolutePath, "*.pr")` which eagerly loads ALL .pr files into memory at startup. PLang uses lazy loading — goals should only be loaded on demand via `GetAsync`. Only setup goals need to be discovered before `Setup.RunAsync`.

## Fix

### 1. Add `DiscoverAsync` to `Setup.@this`

OBP rule 1: behavior belongs to the owner. Setup should discover its own goals.

```csharp
public async Task<Data> DiscoverAsync(Engine.@this engine, CancellationToken ct = default)
```

This method:
- Scans `engine.AbsolutePath` for all `*.pr` files
- Parses each one via `engine.Channels.ReadAsync<Goal.@this>`
- Only adds goals where `IsSetup == true` to the collection
- Discards non-setup goals (they remain lazy-loadable via `GetAsync`)
- Returns `Data.Ok()` or `Data.FromError()` on failure

**Files**: `PLang/App/Goals/Setup/this.cs`

### 2. Replace `LoadFromDirectoryAsync` with `DiscoverAsync` in Executor.Run2

```csharp
// Before:
await engine.Goals.LoadFromDirectoryAsync(engine, engine.AbsolutePath, "*.pr", cancellationToken: cancellationToken);

// After:
await engine.Goals.Setup.DiscoverAsync(engine, cancellationToken);
```

**Files**: `PLang/Executor.cs`

### 3. Tests

- Add `DiscoverAsync_OnlyLoadsSetupGoals` — creates mixed .pr files on disk, verifies only setup goals are in collection
- Add `DiscoverAsync_NonSetupGoalsRemainLazyLoadable` — verifies non-setup goals can still be loaded via `GetAsync`
- Update existing test if needed

**Files**: `PLang.Tests/App/Goals/Setup/SetupTests.cs`

## Verification

All 1490+ C# tests pass. No behavioral change to setup execution — only the discovery mechanism changes.
