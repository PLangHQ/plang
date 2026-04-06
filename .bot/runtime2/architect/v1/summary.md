# Setup.goal — Architect v1 Summary

## What this is

Setup goals run once-per-step at app startup. Every PLang app runs setup before the requested goal. Steps are tracked persistently in system.sqlite so they only execute once in the app's lifetime. New steps run on next startup. Changed steps (different hash) re-run.

## What was done

Designed the Setup system for runtime2 through iterative discussion with Ingi. Traced runtime1's RunSetup/HasExecuted system, identified what to preserve and what to change.

**Key design decisions:**
- `engine.Goals.Setup` becomes a real object (not `IEnumerable<Goal>`) with `Goals`, `Executions`, and `RunAsync`
- `Executions` is a smart collection wrapping a sqlite table — one row per step, keyed by `step.Hash`
- `context.Setup` propagates through goal.call — any step reachable from setup gets run-once semantics
- Steps.RunAsync (new) owns the iteration and the run-once check; Goal.RunAsync delegates
- Setup goals excluded from regular goal lookup
- Error info stored in Executions row for diagnostics; error tolerance policy deferred

**Discrepancy from runtime1:** Runtime1 only tracked steps in `IsSetup` goals. App tracks ALL steps reachable from setup execution (including goals called via goal.call). This was intentional — Ingi confirmed the old behavior was a bug.

**Files to change:**
- `PLang/App/Engine/Goals/this.cs` — Setup property becomes object, exclude setup from regular lookup
- `PLang/App/Engine/Goals/Goal/Methods.cs` — delegate step iteration to Steps.RunAsync
- `PLang/App/Engine/Goals/Goal/Steps/this.cs` — add RunAsync with setup check
- `PLang/App/Engine/Context/PLangContext.cs` — add Setup property, update Clone/CreateChild

**Files to create:**
- `PLang/App/Engine/Goals/Setup/this.cs` — Setup class
- `PLang/App/Engine/Goals/Setup/Executions.cs` — Executions smart collection

**Builder change needed:** Verify Step.Hash composition is `hash(goalPath + compiledAction)` without step index.

## Code example

The core pattern — Steps.RunAsync with run-once check:
```csharp
// Inside Steps.RunAsync (new method)
for (var i = 0; i < Count; i++)
{
    var step = this[i];

    if (context.Setup?.Executions.Contains(step) == true)
        continue;

    var result = await step.RunAsync(engine, context, ct);

    // Record execution (even on tolerated errors)
    context.Setup?.Executions.Add(step, result.Success ? null : result.Error);

    if (!result.Success && !(step.OnError?.IgnoreError ?? false))
        return result;
}
```
