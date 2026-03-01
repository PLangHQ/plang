# v4 Summary — Tester v3 Findings

## What this is
Fixes 3 tester findings: Record() return value now checked (abort on failure), skip test proves skip via data marker, cancellation test added, and setup-tolerable errors ported from runtime1.

## What was done

### F1 (Major): Record failure aborts setup
`Steps.RunAsync` now checks `Record()` return value. If the system DataSource can't persist the execution record, setup aborts — safer to re-run than silently skip.

Added `IsTolerableError` to `Setup.@this` — matches runtime1's behavior of automatically tolerating "already exists" (table/index) and "duplicate column name" errors during setup. These are expected in idempotent re-runs.

### F2 (Minor): Skip test now proves skip
Pre-records step1 with a marker string via raw DataSource. After RunAsync, verifies the marker survives (Record() would overwrite it with metadata). Also verifies step2's record contains metadata (not the marker), proving it was executed.

### F4 (Minor): Cancellation test
Passes a pre-cancelled token to `Setup.RunAsync`, verifies it returns `GoalError.Cancelled`.

## Files modified
- `PLang/Runtime2/Engine/Goals/Setup/this.cs` — added `IsTolerableError` method
- `PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs` — unified tolerance check, Record() return checked
- `PLang.Tests/Runtime2/Goals/Setup/SetupTests.cs` — 7 new/rewritten tests

## Code example

```csharp
// Steps/this.cs — unified tolerance + Record abort
var errorTolerated = stepResult.Success
    || (step.OnError?.IgnoreError ?? false)
    || (context.Setup != null && context.Setup.IsTolerableError(stepResult));

if (context.Setup != null && errorTolerated)
{
    var recordResult = await context.Setup.Record(step, engine, ...);
    if (!recordResult.Success)
        return recordResult;  // abort — can't track execution
}
```

## Test results
- C# tests: 1485/1485 pass
