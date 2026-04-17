# Action Modifiers Design

## The Problem

Today, `onError`, `cache`, and `timeout` are special-cased at every layer:

1. **Step model** — dedicated properties (`Step.OnError`, `Step.Cache`, `Step.Timeout`) with their own classes (`ErrorHandler`, `CacheSettings`)
2. **Builder prompt** — special section explaining modifier syntax separate from actions
3. **LLM output** — special step-level JSON properties outside the actions array
4. **Builder save** — `Merge()` has special handling for these properties
5. **Runtime** — `Step.RunAsync()` has `RunActionsWithTimeout()`, `HandleErrorAsync()`, cache check logic — all hardcoded

Adding a new step modifier (e.g., `parallel`, `async/don't wait`) requires touching all five layers. The system doesn't scale.

Worse: modifiers apply to the **entire step**. If a step has two actions (`file.read` + `variable.set`), caching wraps both — including the pointless `variable.set`. Error handling catches failures from any action with no way to differentiate.

## The Design

**Modifiers become regular actions.** Same `module.action` + `parameters` shape as every other action. They're distinguished by a `[Modifier]` attribute on their handler class, and they live in a `modifiers` array on each action in the .pr file.

### PLang Developer Experience

No change in how steps are written. Natural language, same as today:

```plang
- read file.txt, on 404 call FixFile and retry, write to %content%, cache for 10 min
- foreach %files%, call ProcessFile, run on 10 threads, timeout 30 sec
- send email to %recipient%, don't wait
```

### LLM Output (flat, unstructured)

The LLM outputs a flat list of actions. It doesn't know about modifier grouping — it just maps natural language to `module.action` as it does for everything else:

```json
{
  "steps": [{
    "index": 0,
    "guidance": "file.read with error handling for 404, variable assignment, and caching",
    "actions": [
      {"module": "file", "action": "read", "parameters": [{"name": "Path", "value": "file.txt", "type": "path"}]},
      {"module": "error", "action": "on", "parameters": [{"name": "StatusCode", "value": 404, "type": "int"}, {"name": "Goal", "value": "FixFile", "type": "string"}, {"name": "Retry", "value": true, "type": "bool"}]},
      {"module": "variable", "action": "set", "parameters": [{"name": "Name", "value": "%content%", "type": "string"}]},
      {"module": "cache", "action": "for", "parameters": [{"name": "DurationMs", "value": 600000, "type": "long"}]}
    ],
    "level": "high", "confidence": 92
  }]
}
```

The LLM prompt gets **simpler** — no special "Step Modifiers" section. Modifier modules appear in `%actionSummary%` alongside all other modules. The LLM just picks from the full registry.

### Builder Restructures on Save (deterministic C#)

After the LLM returns the flat list, the builder's save pipeline restructures it into grouped form. This is deterministic code, not LLM judgment:

1. Walk the flat action list
2. Classify each action: check if its handler has `[Modifier]`
3. Attach each modifier to the most recent executable action
4. Sort modifiers by their `[Modifier(Order = N)]` — outermost wrapper first
5. Write the structured .pr file

### .pr File (structured, what runtime sees)

```json
{
  "steps": [{
    "index": 0,
    "text": "read file.txt, on 404 call FixFile and retry, write to %content%, cache for 10 min",
    "actions": [
      {
        "module": "file",
        "action": "read",
        "parameters": [{"name": "Path", "value": "file.txt", "type": "path"}],
        "modifiers": [
          {"module": "cache", "action": "for", "parameters": [{"name": "DurationMs", "value": 600000, "type": "long"}]},
          {"module": "error", "action": "on", "parameters": [{"name": "StatusCode", "value": 404, "type": "int"}, {"name": "Goal", "value": "FixFile", "type": "string"}, {"name": "Retry", "value": true, "type": "bool"}]}
        ]
      },
      {
        "module": "variable",
        "action": "set",
        "parameters": [{"name": "Name", "value": "%content%", "type": "string"}],
        "modifiers": []
      }
    ]
  }]
}
```

Key points:
- Modifiers are ordered outermost-first: `cache` (skip everything if cached) before `error` (try/catch around the action)
- Each action carries its own modifiers — per-action, not per-step
- Modifiers follow the same `module.action` + `parameters` shape as executable actions
- The runtime never sorts or classifies — the .pr file is the execution plan

### Runtime Execution

`Action.RunAsync()` becomes a fold over modifiers:

```csharp
public async Task<Data> RunAsync(Context context)
{
    // Build execution chain: innermost = the actual dispatch
    Func<Task<Data>> execute = () => Dispatch(context);

    // Fold modifiers right-to-left (innermost first in the list = last to wrap)
    for (int i = Modifiers.Count - 1; i >= 0; i--)
        execute = Modifiers[i].Wrap(execute, context);

    return await execute();
}
```

Each modifier handler implements `Wrap`:

```csharp
// Cache modifier
public Func<Task<Data>> Wrap(Func<Task<Data>> next, Context context)
{
    return async () =>
    {
        var cached = await cache.GetAsync(key);
        if (cached != null) return cached;
        var result = await next();
        if (result.Success) await cache.SetAsync(key, result, DurationMs);
        return result;
    };
}

// Error modifier
public Func<Task<Data>> Wrap(Func<Task<Data>> next, Context context)
{
    return async () =>
    {
        var result = await next();
        if (!result.Success) result = await HandleError(result, context);
        return result;
    };
}

// Timeout modifier
public Func<Task<Data>> Wrap(Func<Task<Data>> next, Context context)
{
    return async () =>
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        cts.CancelAfter(Ms);
        context.PushCancellation(cts);
        try { return await next(); }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return Data.FromError(new ServiceError($"Timed out after {Ms}ms", "Timeout", 408));
        }
        finally { context.PopCancellation(); }
    };
}
```

### Modifier Nesting Order

The builder sorts modifiers by `Order` before writing the .pr file. The runtime processes them in list order (right-to-left fold), so the first modifier in the list becomes the outermost wrapper:

| Order | Module   | Role | Why this position |
|-------|----------|------|-------------------|
| 0     | async    | Fire-and-forget | Outermost — decides whether to even await |
| 1     | timeout  | Hard deadline | Caps total time including cache lookup |
| 2     | cache    | Skip if cached | No need to error-handle if cache hits |
| 3     | error    | Try/catch | Closest to the action, handles its failures |
| 4     | parallel | Iteration strategy | Modifies how foreach loops execute |

The execution nesting for a fully-modified action:

```
async(                          ← fire-and-forget?
  timeout(5000ms,               ← hard deadline
    cache(10min,                ← return cached if available
      error(on 404 call Fix,   ← handle action failure
        file.read()             ← the actual operation
      )
    )
  )
)
```

### The [Modifier] Attribute

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ModifierAttribute : Attribute
{
    /// <summary>
    /// Nesting order. Lower = outermost wrapper. 
    /// The builder sorts modifiers by this before writing .pr files.
    /// </summary>
    public int Order { get; init; }
}

// Usage on handler classes:
[Modifier(Order = 1)]
public partial class SetHandler : BaseClass<set> { ... }  // timeout.set

[Modifier(Order = 2)]
public partial class ForHandler : BaseClass<@for> { ... }  // cache.for

[Modifier(Order = 3)]
public partial class OnHandler : BaseClass<on> { ... }     // error.on
```

### IModifier Interface

All modifier handlers implement a shared interface that the runtime uses for the fold:

```csharp
public interface IModifier
{
    Func<Task<Data>> Wrap(Func<Task<Data>> next, Context context);
}
```

This is the only contract between the runtime and modifiers. The runtime doesn't know what cache, error, or timeout do — it just calls `Wrap`.

### Action Model Changes

The `Action` class gains a `Modifiers` property:

```csharp
public sealed partial class @this : Data.@this<@this>
{
    // ... existing properties ...

    [Store, Debug, Default]
    public List<@this> Modifiers { get; init; } = new();
}
```

Modifiers are actions themselves — same class. They just happen to have handlers that implement `IModifier`.

### Step Model Changes

The Step class **loses** its special properties:

```csharp
// REMOVED:
// public ErrorHandler? OnError { get; set; }
// public CacheSettings? Cache { get; set; }
// public int? Timeout { get; init; }
```

`ErrorHandler.cs` and `CacheSettings.cs` become obsolete — their data now lives in modifier action parameters (`error.on` and `cache.for` parameter records).

`Step.RunAsync()` simplifies to:

```csharp
public async Task<Data> RunAsync(Context context)
{
    context.Step = this;
    var lifecycle = context.LifecycleFor(this);

    var beforeResult = await lifecycle.Before.Run(context, EventType.BeforeStep);
    if (!beforeResult.Success) return beforeResult;
    if (beforeResult.Handled) return beforeResult;

    var result = await RunActions(context);

    var afterResult = await lifecycle.After.Run(context, EventType.AfterStep);
    if (!afterResult.Success) return afterResult;

    return result;
}
```

No timeout wrapping, no error handling, no cache checks. All of that is inside each action's modifier fold.

### Builder Prompt Changes

The "Step Modifiers" section in `BuildGoal.llm` gets removed entirely. Instead, modifier modules appear in `%actionSummary%` like any other module:

```
error.on — Handle action errors. Properties: StatusCode (int?), Key (string?), 
           Message (string?), Goal (string?), RetryCount (int?), RetryOverMs (int?), 
           Order (ErrorOrder?), IgnoreError (bool)
cache.for — Cache action result. Properties: DurationMs (long), Sliding (bool?), 
            Key (string?), Location (string?)
timeout.set — Set action timeout. Properties: Ms (int)
```

The LLM just picks these from the action registry like it picks `file.read` or `output.write`. No special instructions needed.

### Builder Save Pipeline

A new step in the save pipeline (after LLM response, before writing .pr):

```csharp
public static List<Action> GroupModifiers(List<Action> flatActions, ModuleRegistry registry)
{
    var grouped = new List<Action>();
    Action? current = null;

    foreach (var action in flatActions)
    {
        if (registry.IsModifier(action.Module, action.ActionName))
        {
            // Attach to most recent executable action
            current?.Modifiers.Add(action);
        }
        else
        {
            current = action;
            grouped.Add(current);
        }
    }

    // Sort each action's modifiers by Order
    foreach (var action in grouped)
        action.Modifiers.Sort((a, b) =>
            registry.GetModifierOrder(a) - registry.GetModifierOrder(b));

    return grouped;
}
```

### Future Modifiers

This design is extensible. Adding a new modifier requires:

1. **Write the handler class** with `[Modifier(Order = N)]` and `IModifier.Wrap()`
2. **Register it** in the module registry

That's it. No Step changes, no runtime changes, no builder prompt changes (it auto-discovers via `%actionSummary%`), no .pr schema changes.

Planned future modifiers:

| Module | Action | Description | Order |
|--------|--------|-------------|-------|
| `async` | `fire` | Don't wait for result | 0 |
| `parallel` | `set` | Run N concurrent iterations (foreach) | 4 |
| `retry` | `on` | Simple retry without error goal (subset of error.on) | 3 |
| `throttle` | `set` | Rate-limit action execution | 2 |

### Migration Path

1. **Phase 1** — Add `Modifiers` list to Action. Create `error`, `cache`, `timeout` modifier modules with `[Modifier]` attribute and `IModifier` interface. Update `Action.RunAsync()` to fold modifiers. **Keep** Step.OnError/Cache/Timeout for backward compat with existing .pr files.

2. **Phase 2** — Update builder prompt: remove "Step Modifiers" section, let modifier modules appear in action summary. Update builder save pipeline to restructure flat LLM output into grouped .pr format. New builds produce the new format.

3. **Phase 3** — Add GoalMapper migration: when loading old .pr files with step-level `onError`/`cache`/`timeout`, map them into action-level modifiers. Remove Step.OnError/Cache/Timeout properties.

4. **Phase 4** — Add new modifier modules (`async`, `parallel`, etc.) — each is just a handler + attribute, no other changes needed.

### What This Solves

- **No more special cases** — modifiers are actions, same pipeline as everything else
- **Per-action precision** — cache just the `file.read`, not the `variable.set` after it
- **Composable** — multiple error handlers for different actions in one step
- **Extensible** — new modifiers are just handler classes, no system-wide changes
- **Simpler LLM prompt** — no special modifier section, just the action registry
- **Simpler Step class** — no timeout/error/cache properties or handling code
- **Builder does the structuring** — runtime gets pre-sorted, pre-grouped .pr files

### Open Questions

1. **Modifier on modifier?** Can you put a timeout on an error handler's goal call? Probably not in v1 — modifiers modify executable actions only. But the Wrap pattern doesn't prevent it structurally.

2. **Source generator** — Does the lazy param generator need to change? Modifier actions have parameters just like executable actions, so it should work as-is. The `IModifier.Wrap()` method is hand-written per modifier, not generated.

3. **Events** — Should modifiers fire Before/After events? Probably not — they're transparent wrappers. The action's own events still fire inside the fold.

4. **`parallel` modifier** — This one's different because it changes iteration strategy, not wrapping a single action. Needs a slightly different interface, maybe `IIterationModifier` alongside `IModifier`. Design this when we get there.
