# Setup.goal Implementation Plan

## What This Is

Setup goals run once-per-step at app startup. Steps are tracked persistently in the `setup` table of `engine.System.DataSource` (system.sqlite), keyed by `step.Hash`. If a developer adds a new step, only that step runs on next startup. If a step's hash changes (code changed), it re-runs.

## Files to Create

### 1. `PLang/Runtime2/Engine/Goals/Setup/this.cs` — Setup class

The Setup object. Replaces `IEnumerable<Goal> Setup` on EngineGoals. Uses `engine.System.DataSource` for persistence — no new sqlite code. Each step gets one row in the `setup` table keyed by hash.

```csharp
namespace PLang.Runtime2.Engine.Goals.Setup;

public sealed class @this
{
    private readonly EngineGoals _goals;

    public @this(EngineGoals goals) { _goals = goals; }

    // Lazy-filtered setup goals (ordered: "Setup" first, then alphabetical)
    public IEnumerable<Goal.@this> Goals => _goals.All
        .Where(g => g.IsSetup)
        .OrderBy(g => g.Name.Equals("Setup", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
        .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase);

    private const string Table = "setup";

    public async Task<Data> RunAsync(Engine.@this engine, PLangContext context, CancellationToken ct = default)
    {
        context.Setup = this;
        try
        {
            foreach (var goal in Goals)
            {
                var loadResult = await goal.Load(context);
                if (!loadResult.Success) return loadResult;
                var result = await goal.RunAsync(engine, context, ct);
                if (!result.Success) return result;
            }
            return Data.Ok();
        }
        finally
        {
            context.Setup = null;
        }
    }

    // Called from Steps.RunAsync — check if step was already executed
    public async Task<bool> IsExecuted(Step step, Engine.@this engine)
    {
        if (string.IsNullOrEmpty(step.Hash)) return false;
        var result = await engine.System.DataSource.Exists(Table, step.Hash);
        return result.Success && result.Value is true;
    }

    // Called from Steps.RunAsync — record step execution
    public async Task Record(Step step, Engine.@this engine, IError? error = null)
    {
        if (string.IsNullOrEmpty(step.Hash)) return;
        var metadata = new {
            goalPath = step.Goal?.Path,
            stepIndex = step.Index,
            stepText = step.Text,
            executedAt = DateTime.UtcNow.ToString("O"),
            error = error?.Message
        };
        await engine.System.DataSource.Set(Table, step.Hash, metadata);
    }
}
```

## Files to Modify

## Files to Modify

### 2. `PLang/Runtime2/Engine/Goals/this.cs` (EngineGoals)

- **Line 186**: Change `Setup` from `IEnumerable<Goal.@this>` filter to `Setup.@this` object
- Remove the old `Setup => _goals.Values.Where(g => g.IsSetup)` property
- Add: `public Setup.@this Setup { get; }` initialized in constructor
- Exclude setup goals from regular lookup in `Get()` and `GetAsync()` — a developer cannot accidentally call a setup goal from normal code

### 3. `PLang/Runtime2/Engine/Context/PLangContext.cs`

- **Add property**: `public Setup.@this? Setup { get; set; }` (nullable, set only during setup execution)
- **Clone() line 194**: Copy `Setup` to clone
- **CreateChild() line 184**: No change needed — child context created via constructor which doesn't copy Setup; that's correct since goal.call creates a child with the same parent chain, and Setup propagates via the parent

Wait — actually, looking at how goal.call works: `Goal.RunAsync` doesn't create a child context, it saves/restores context fields. So `context.Setup` will naturally propagate through goal.call. No `CreateChild` change needed.

Actually, re-reading the architect's spec: "Context carries Setup through the parent chain. Any goal called from within setup execution inherits the setup context." This means `context.Setup` is simply set at the top and stays set during the entire setup execution tree. Since `Goal.RunAsync` uses the same context object (saves/restores Goal and Step but doesn't create a new context), this just works.

But `Clone()` must still copy Setup for completeness.

### 4. `PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs` (Steps)

- **Add `RunAsync`** method that owns the step iteration loop (moved from Goal.Methods.cs)
- The setup run-once check goes here:
  ```csharp
  if (context.Setup != null && await context.Setup.IsExecuted(step, engine))
      continue;
  // ... run step ...
  if (context.Setup != null)
      await context.Setup.Record(step, engine, result.Success ? null : result.Error);
  ```

### 5. `PLang/Runtime2/Engine/Goals/Goal/Methods.cs` (Goal.RunAsync)

- **Lines 57-70**: Replace the step iteration loop with delegation to `Steps.RunAsync(engine, context, cancellationToken)`
- This follows OBP rule 5: "Collections are smart wrappers... Parents delegate — they never iterate directly"

### 6. `PLang/Executor.cs` — Entry point (Run2 method)

- **Line 366**: Before `engine.Goals.Run(goalName, ...)`, call `engine.Goals.Setup.RunAsync(engine, context, ct)`
- When goalName == "setup" (case-insensitive), only run setup goals, don't run the main goal

## Step.Hash Verification

The architect notes to verify that Step.Hash is `hash(goalPath + compiledAction)` without step index. Looking at `Step/this.cs:34`, `Hash` exists as `string?`. The hash is computed at build time by the builder. For now, I'll trust the builder's hash composition and not modify it. If step index is included, reordering triggers re-runs — acceptable for v1, can be refined later.

## C# Tests

### `PLang.Tests/Runtime2/Goals/Setup/SetupTests.cs`

1. **Setup_RunAsync_SkipsAlreadyExecutedSteps** — Run setup twice, verify steps only execute once
2. **Setup_RunAsync_RunsNewStepsOnly** — Run setup, add a step, run again, verify only new step runs
3. **Setup_RunAsync_RerunsChangedHash** — Run setup, change hash, run again, verify step re-runs
4. **Setup_PropagatesThroughGoalCall** — context.Setup flows through goal.call
5. **Executions_Contains_ReturnsFalseForNewStep** — Fresh table, step not found
6. **Executions_Add_ThenContains_ReturnsTrue** — Add step, verify found
7. **Setup_GoalsOrder** — "Setup" goal comes first, then alphabetical

### PLang Integration Test

`Tests/Runtime2/Setup/Start.test.goal`:
```
Start
- set %counter% = 0
- call Setup
- call Setup
- assert %counter% equals 1, "setup step should only run once"
```

With a `Setup.goal`:
```
Setup
- set %counter% = %counter% + 1
```

This is tricky because PLang test runner creates a fresh engine per test. The setup goals would need to be loaded. Let me think about this... Actually, the PLang test might be complex to set up properly since setup execution requires the system.sqlite tracking. I'll write C# tests first, and consider PLang tests after.

## Execution Order

1. Create `Setup/this.cs` (single file, uses engine.System.DataSource)
2. Add `Setup` property to `PLangContext`
3. Modify `EngineGoals` to expose `Setup` object
4. Add `Steps.RunAsync` with setup check
5. Refactor `Goal.RunAsync` to delegate to `Steps.RunAsync`
6. Wire up in `Executor.Run2`
7. Write C# tests
8. Build and test

## Risks

- **Low**: The Step.Hash might include step index (would cause re-runs on reorder). Acceptable for v1.
- **Medium**: Excluding setup goals from regular lookup could break existing code that inspects all goals. Need to check `EngineGoals.All` and `EngineGoals.Value` usage.
