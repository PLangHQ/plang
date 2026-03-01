# v2 Summary — Code Analyzer Fixes

## What this is
Fixes for 3 findings from the code analyzer review of v1's Setup.goal run-once execution system. The most critical fix ensures failed setup steps are NOT permanently recorded as executed (they need to re-run on next startup).

## What was done

### Finding 1 (High): Failed setup steps permanently marked as executed
**Problem**: Steps.RunAsync recorded execution BEFORE checking if the error should propagate. A transient failure in "create table users" would be recorded, and the step would be skipped forever on subsequent startups.

**Fix**: In `PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs`, moved the record call AFTER step execution and only record on success or when the error is explicitly tolerated (`OnError.IgnoreError`):
```csharp
if (context.Setup != null)
{
    var tolerated = stepResult.Success || (step.OnError?.IgnoreError ?? false);
    if (tolerated)
        await context.Setup.Record(step, engine, stepResult.Success ? null : stepResult.Error);
}
```

### Finding 2 (Medium): Setup.Record silently swallows DataSource errors
**Problem**: `Record` returned `Task` instead of `Task<Data>`, so if `DataSource.Set` failed (disk full, locked), nobody knew.

**Fix**: In `PLang/Runtime2/Engine/Goals/Setup/this.cs`, changed return type to `Task<Data>` and returns the DataSource result directly.

### Finding 3 (Low): Count/All include setup goals, but Get excludes them
**Problem**: `engine.Goals.Count` counted setup goals, but `engine.Goals.Get(name)` couldn't find them — inconsistent API surface.

**Fix**: In `PLang/Runtime2/Engine/Goals/this.cs`:
- Added `AllIncludingSetup` internal property for `Setup.Goals` to use
- Filtered `All`, `Count`, and `Value` to exclude setup goals, consistent with `Get()`

### Tests added
- `RunAsync_FailedStepNotRecorded` — verifies a failed step is NOT recorded in the setup table
- `RunAsync_ToleratedErrorStepIsRecorded` — verifies a step with `OnError.IgnoreError = true` IS recorded even though it failed

## Files modified
- `PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs` — record-on-failure logic
- `PLang/Runtime2/Engine/Goals/Setup/this.cs` — Record returns Task<Data>
- `PLang/Runtime2/Engine/Goals/this.cs` — AllIncludingSetup, filtered All/Count/Value
- `PLang.Tests/Runtime2/Goals/Setup/SetupTests.cs` — 2 new tests

## Test results
- C# tests: 1476/1476 pass
- PLang tests: 23/23 pass
