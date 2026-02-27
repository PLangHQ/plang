# Code Analyzer v1 Summary — runtime2-setup-goal

## What this is

Analysis of the Setup.goal run-once execution system. This feature lets PLang apps define setup goals that execute once per step (tracked by hash in SQLite). Steps that have already run are skipped on subsequent startups. Changed steps (different hash) re-run automatically.

## What was done

5-pass analysis of all new and modified code. Three findings:

1. **Failed setup steps permanently marked as executed (High)** — Steps.RunAsync records the step execution before checking whether the error should propagate. A step that fails due to a transient issue (permissions, disk full) is permanently marked as done and never retried. Fix: only record on success or when IgnoreError is true.

2. **Setup.Record silently swallows errors (Medium)** — Record returns `Task` (void), not `Task<Data>`. If the DataSource.Set call fails, no error propagates and no diagnostics are available. Fix: return `Task<Data>`.

3. **EngineGoals.Count/All include setup goals but Get excludes them (Low)** — Inconsistency between collection-level properties and lookup methods. Not a bug in current usage but a trap for future callers.

## Code example

The core issue (Finding 1) in `Steps/this.cs`:

```csharp
// CURRENT — records before error check
var stepResult = await step.RunAsync(engine, context, cancellationToken);
if (context.Setup != null)
    await context.Setup.Record(step, engine, ...);  // always records
if (!stepResult) return stepResult;  // too late — already recorded

// FIX — only record on success or tolerated error
var stepResult = await step.RunAsync(engine, context, cancellationToken);
if (context.Setup != null)
{
    if (stepResult.Success || (step.OnError?.IgnoreError ?? false))
        await context.Setup.Record(step, engine, ...);
}
```

## Verdict: FAIL — two behavioral issues need fixing before merge.
