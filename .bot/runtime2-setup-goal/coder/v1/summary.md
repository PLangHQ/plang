# v1 Summary: Setup.goal Run-Once Execution System

## What this is

Setup goals run once-per-step at app startup. Steps are tracked persistently in the `setup` table of `engine.System.DataSource` (system.sqlite), keyed by `step.Hash`. If a developer adds a new step, only that step runs on next startup. If a step's hash changes (code changed), it re-runs.

## What was done

### New file: `PLang/App/Goals/Setup/this.cs`
The Setup object. Contains:
- `Goals` — filtered+ordered setup goals ("Setup" first, then alphabetical)
- `RunAsync(engine, context, ct)` — sets `context.Setup`, iterates goals, clears when done
- `IsExecuted(step, engine)` — checks `engine.System.DataSource.Exists("setup", step.Hash)`
- `Record(step, engine, error?)` — writes step metadata to `engine.System.DataSource.Set("setup", step.Hash, metadata)`

No separate `Executions` class — uses the existing System actor's DataSource directly.

### Modified files:
- **`PLang/App/Goals/this.cs`** (EngineGoals) — `Setup` property changed from `IEnumerable<Goal>` to `Setup.@this` object. Added constructor. Setup goals excluded from regular `Get()` lookup (can't be called from normal code).
- **`PLang/App/Context/PLangContext.cs`** — Added `Setup` property (nullable). Included in `Clone()`.
- **`PLang/App/Goals/Goal/Steps/this.cs`** (Steps) — Added `RunAsync` method that owns step iteration loop (moved from Goal). Includes setup run-once check: skip if already executed, record after running.
- **`PLang/App/Goals/Goal/Methods.cs`** (Goal.RunAsync) — Replaced inline step loop with `Steps.RunAsync(engine, context, ct)` delegation (OBP rule 5).
- **`PLang/Executor.cs`** (Run2) — Calls `Setup.RunAsync` before main goal. When goalName is "setup", only runs setup.
- **`PLang.Tests/App/Core/GoalsTests.cs`** — Updated existing test to use `goals.Setup.Goals.ToList()`.

### New test file: `PLang.Tests/App/Goals/Setup/SetupTests.cs`
9 tests covering:
- Goal ordering (Setup first, then alphabetical)
- Setup goals excluded from regular lookup
- IsExecuted returns false for new/null-hash steps
- Record then IsExecuted returns true
- RunAsync skips already-executed steps
- Hash change triggers re-run
- context.Setup set during execution, cleared after
- Clone preserves Setup

## Code example

The core pattern — Steps.RunAsync with run-once check:
```csharp
// Inside Steps.RunAsync
for (var i = 0; i < Count; i++)
{
    var step = this[i];

    if (context.Setup != null && await context.Setup.IsExecuted(step, engine))
        continue;

    var stepResult = await step.RunAsync(engine, context, cancellationToken);

    if (context.Setup != null)
        await context.Setup.Record(step, engine, stepResult.Success ? null : stepResult.Error);

    if (!stepResult)
    {
        if (!(step.OnError?.IgnoreError ?? false))
            return stepResult;
    }
}
```

## Results
- C# tests: 1474/1474 pass (9 new setup tests)
- PLang tests: 23/23 pass
