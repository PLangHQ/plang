# Action Modifiers — Implementation Roadmap

This roadmap breaks the action modifiers design into four phases. Each phase is independently shippable. Phases 1-2 are the core work; phases 3-4 are cleanup and expansion.

Read `plan.md` in this directory for the full design rationale. This document is the implementation spec.

**Updated 2026-04-16:** Reconciled with runtime2 HEAD after large merge. Key findings:
- `Action.Return` property has been **removed** — variable assignment is now an explicit `variable.set` action via `%__data__%` flow
- `cache.check`, `cache.store`, and `error.check` action handlers **already exist** in `PLang/App/modules/cache/` and `PLang/App/modules/error/` — but they're called as step-orchestration actions (they receive `Step` as a parameter), not as per-action modifiers
- `DefaultBuilderProvider.cs` has grown significantly with validation, `ValidateResponse`, `PromoteGroups`, `IBuildValidatable`, and goal-call path resolution
- Builder prompt now uses `{{ }}` template syntax instead of `%var%`
- The LLM schema in `BuildGoal.goal` (line 23) still has `cache` and `onError` as step-level properties
- `Step.RunAsync()` still has inline `HandleErrorAsync()` and `RunActionsWithTimeout()` — these haven't been refactored to use the existing action handlers yet

---

## Phase 1: Runtime — Modifier Infrastructure

**Goal:** The runtime can execute actions with modifiers attached. Existing .pr files still work unchanged.

### 1.1 `IModifier` Interface

**Create:** `PLang/App/modules/IModifier.cs`

```csharp
namespace App.modules;

/// <summary>
/// Contract for action modifiers. Modifiers wrap an action's execution
/// by receiving and returning a delegate. The runtime folds modifiers
/// right-to-left: first in list = outermost wrapper.
/// </summary>
public interface IModifier
{
    Func<Task<Data.@this>> Wrap(Func<Task<Data.@this>> next, Actor.Context.@this context);
}
```

This is the **only** contract between the runtime and modifiers.

### 1.2 `[Modifier]` Attribute

**Create:** `PLang/App/modules/ModifierAttribute.cs`

```csharp
namespace App.modules;

[AttributeUsage(AttributeTargets.Class)]
public class ModifierAttribute : Attribute
{
    /// <summary>
    /// Nesting order. Lower = outermost wrapper.
    /// Builder sorts modifiers by this before writing .pr files.
    /// </summary>
    public int Order { get; init; }
}
```

### 1.3 `Modifiers` Property on Action

**Modify:** `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs`

Add property:

```csharp
[Store, Debug, Default]
public List<@this> Modifiers { get; init; } = new();
```

Modifiers are actions — same class. Their handlers happen to implement `IModifier`.

Also update `Clone()` in `Step/this.cs` (line 268 area) to clone modifiers on each action:

```csharp
Modifiers = new List<Action>(a.Modifiers.Select(m => new Action
{
    Module = m.Module,
    ActionName = m.ActionName,
    Parameters = new List<Data.@this>(m.Parameters),
    Modifiers = new List<Action>() // modifiers on modifiers not supported v1
}))
```

### 1.4 Action.RunAsync — Modifier Fold

**Modify:** `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — `RunAsync` method (lines 68-92)

The current dispatch at line 78 (`var result = await context.App!.Run(this, context)`) becomes a fold when modifiers are present:

```csharp
public async Task<Data.@this> RunAsync(Actor.Context.@this context)
{
    var lifecycle = context.LifecycleFor(this);

    // BeforeAction events
    var beforeResult = await lifecycle.Before.Run(context, App.Events.EventType.BeforeAction);
    if (!beforeResult.Success) return beforeResult;
    if (beforeResult.Handled) return beforeResult;

    // Build execution chain: innermost = dispatch to handler
    Func<Task<Data.@this>> execute = () => context.App!.Run(this, context);

    // Fold modifiers right-to-left (first in list = outermost wrapper)
    if (Modifiers.Count > 0)
    {
        var app = context.App!;
        for (int i = Modifiers.Count - 1; i >= 0; i--)
        {
            var modifier = Modifiers[i];
            var (handler, error) = app.Modules.GetCodeGenerated(modifier);
            if (error != null) return Data.@this.FromError(error);
            if (handler is not IModifier mod)
                return Data.@this.FromError(new Errors.ServiceError(
                    $"{modifier.Module}.{modifier.ActionName} is not a modifier", "ModifierError", 400));

            // Initialize modifier: resolve params via generated ExecuteAsync
            await handler.ExecuteAsync(modifier, context);

            var next = execute; // capture for closure
            execute = mod.Wrap(next, context);
        }
    }

    var result = await execute();

    // Store result as %__data__% — available to next action or step
    if (result.Success)
    {
        result.Name = "__data__";
        context.Variables.Put(result);
    }

    // AfterAction events
    var afterResult = await lifecycle.After.Run(context, App.Events.EventType.AfterAction);
    if (!afterResult.Success) return afterResult;

    return result;
}
```

**Key design choice:** `handler.ExecuteAsync(modifier, context)` is called first to initialize the modifier (resolve parameters, set properties via the source generator). Then `mod.Wrap(next, context)` builds the wrapper using those resolved properties. The modifier's `Run()` method returns `Data.Ok()` — all real work happens in `Wrap`.

### 1.5 Three Modifier Modules

These are **new handlers** alongside the existing `cache.check`/`cache.store`/`error.check` handlers. The existing handlers work at the step level (they receive `Step` as a parameter). The new modifier handlers work at the action level.

**Important:** The existing `cache/` and `error/` module folders already have handlers. The new modifier actions must not conflict with existing action names. Use distinct names.

#### 1.5.1 `timeout.after`

**Create:** `PLang/App/modules/timeout/after.cs`

This is a new module folder — no conflicts.

```csharp
namespace App.modules.timeout;

[Action("after"), Modifier(Order = 1)]
public partial class after : IModifier, IContext
{
    public partial int Ms { get; init; }

    public Task<Data.@this> Run() => Task.FromResult(Data.@this.Ok());

    public Func<Task<Data.@this>> Wrap(Func<Task<Data.@this>> next, Actor.Context.@this context)
    {
        var ms = Ms;
        return async () =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(ms);
            context.PushCancellation(cts);
            try
            {
                return await next();
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested)
            {
                return Data.@this.FromError(new Errors.ServiceError(
                    $"Timed out after {ms}ms", "Timeout", 408));
            }
            finally
            {
                context.PopCancellation();
            }
        };
    }
}
```

#### 1.5.2 `cache.wrap`

**Create:** `PLang/App/modules/cache/wrap.cs`

Uses name `wrap` to avoid conflict with existing `cache.check` and `cache.store`.

```csharp
namespace App.modules.cache;

[Action("wrap"), Modifier(Order = 2)]
public partial class wrap : IModifier, IContext
{
    public partial long DurationMs { get; init; }
    [Default(false)]
    public partial bool Sliding { get; init; }
    public partial string? Key { get; init; }

    public Task<Data.@this> Run() => Task.FromResult(Data.@this.Ok());

    public Func<Task<Data.@this>> Wrap(Func<Task<Data.@this>> next, Actor.Context.@this context)
    {
        var cacheKey = Key ?? DefaultCacheKey(context);
        var durationMs = DurationMs;
        var sliding = Sliding;

        return async () =>
        {
            var cache = context.App!.Cache;
            var cached = await cache.GetAsync(cacheKey);
            if (cached != null)
            {
                // Restore __data__ from cache
                cached.Name = "__data__";
                context.Variables.Put(cached);
                return cached;
            }

            var result = await next();
            if (result.Success)
            {
                var settings = new App.Goals.Goal.Steps.Step.CacheSettings
                {
                    DurationMs = durationMs,
                    Sliding = sliding
                };
                await cache.SetAsync(cacheKey, result, settings);
            }
            return result;
        };
    }

    private string DefaultCacheKey(Actor.Context.@this context)
    {
        var step = context.Step;
        var goalPath = step?.Goal?.Path ?? "unknown";
        return $"step:{goalPath}:{step?.Index}";
    }
}
```

**Note:** The coder must verify `cache.SetAsync` signature. The existing `cache.store` handler calls `Context.App!.Cache.SetAsync(key, entry, Step.Cache)` with a `CacheSettings` argument. Follow the same pattern.

#### 1.5.3 `error.handle`

**Create:** `PLang/App/modules/error/handle.cs`

Uses name `handle` to avoid conflict with existing `error.check` and `error.throw`.

```csharp
namespace App.modules.error;

[Action("handle"), Modifier(Order = 3)]
public partial class handle : IModifier, IContext
{
    public partial int? StatusCode { get; init; }
    public partial string? Key { get; init; }
    public partial string? Message { get; init; }
    public partial string? Goal { get; init; }
    public partial int? RetryCount { get; init; }
    public partial int? RetryOverMs { get; init; }
    public partial string? Order { get; init; }  // "GoalFirst" or "RetryFirst"
    [Default(false)]
    public partial bool IgnoreError { get; init; }

    public Task<Data.@this> Run() => Task.FromResult(Data.@this.Ok());

    public Func<Task<Data.@this>> Wrap(Func<Task<Data.@this>> next, Actor.Context.@this context)
    {
        return async () =>
        {
            var result = await next();
            if (result.Success) return result;

            // Check if this error matches our filter
            if (!MatchesError(result.Error)) return result;

            if (IgnoreError) return Data.@this.Ok();

            var order = Order == "GoalFirst"
                ? App.Goals.Goal.Steps.Step.ErrorOrder.GoalFirst
                : App.Goals.Goal.Steps.Step.ErrorOrder.RetryFirst;

            if (order == App.Goals.Goal.Steps.Step.ErrorOrder.GoalFirst)
            {
                if (Goal != null)
                {
                    var goalResult = await CallErrorGoal(result, context);
                    if (goalResult.Success) return goalResult;
                }
                var retryResult = await Retry(next, context);
                if (retryResult != null && retryResult.Success) return retryResult;
                if (Goal != null) return Data.@this.Ok(); // Goal ran = handled
            }
            else
            {
                var retryResult = await Retry(next, context);
                if (retryResult != null && retryResult.Success) return retryResult;
                if (Goal != null)
                {
                    await CallErrorGoal(result, context);
                    return Data.@this.Ok();
                }
            }

            return result;
        };
    }

    private bool MatchesError(IError? error)
    {
        if (StatusCode == null && Key == null && Message == null)
            return true; // no filter = match all errors
        if (error == null) return false;

        if (StatusCode != null && error.StatusCode != StatusCode) return false;
        if (Key != null && !string.Equals(error.Key, Key, StringComparison.OrdinalIgnoreCase)) return false;
        if (Message != null && !error.Message.Contains(Message, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private async Task<Data.@this?> Retry(Func<Task<Data.@this>> action, Actor.Context.@this context)
    {
        if (RetryCount == null || RetryCount <= 0) return null;
        var delayMs = RetryOverMs != null && RetryCount > 0
            ? RetryOverMs.Value / RetryCount.Value : 0;

        for (int attempt = 0; attempt < RetryCount; attempt++)
        {
            if (delayMs > 0) await Task.Delay(delayMs, context.CancellationToken);
            var result = await action();
            if (result.Success) return result;
        }
        return null;
    }

    private async Task<Data.@this> CallErrorGoal(Data.@this failedResult, Actor.Context.@this context)
    {
        var goalCall = new App.Goals.Goal.Steps.Step.GoalCall
        {
            Name = Goal!,
            Parameters = new List<Data.@this> { new Data.@this("!error", failedResult.Error) }
        };
        // Stamp Action for sub-goal navigation
        goalCall.Action ??= context.Step?.Actions.FirstOrDefault();
        return await context.App!.RunGoalAsync(goalCall, context);
    }
}
```

**Important:** Compare the retry and error goal logic carefully with the existing `error.check` handler (`PLang/App/modules/error/check.cs` lines 79-132). The new handler wraps a delegate (`next()`) instead of re-running `Step.Actions`, which is the key difference — but the error matching, retry delay, and goal call patterns should be identical.

### 1.6 Module Registry — Modifier Awareness

**Modify:** `PLang/App/Modules/this.cs`

Add methods for modifier classification. These are used by the builder's save pipeline (Phase 2):

```csharp
public bool IsModifier(string module, string action)
{
    var type = GetActionType(module, action);
    return type?.GetCustomAttribute<ModifierAttribute>() != null;
}

public int GetModifierOrder(string module, string action)
{
    var type = GetActionType(module, action);
    var attr = type?.GetCustomAttribute<ModifierAttribute>();
    return attr?.Order ?? int.MaxValue;
}
```

**Note:** `GetActionType(module, action)` should already exist (used in `DefaultBuilderProvider.Validate()` line 230). The coder should verify the exact method name.

Also update `Describe()` to mark modifier actions in the action summary so the LLM knows they wrap preceding actions, not execute in sequence. Add `"(modifier — wraps preceding action)"` to the description.

### 1.7 Keep Step-Level Properties (Backward Compat)

Do **NOT** remove `Step.OnError`, `Step.Cache`, `Step.Timeout` in this phase. Existing .pr files use them. The step-level handling in `Step.RunAsync()` stays as a fallback.

The runtime now has two paths:
- **New .pr files**: modifiers on actions → fold in `Action.RunAsync()`
- **Old .pr files**: step-level properties → existing `Step.RunAsync()` logic

Both paths work. No migration needed yet.

### Phase 1 Tests

**C# unit tests:**
- `IModifier.Wrap` fold with 0, 1, 2, 3 modifiers — verify nesting order
- `timeout.after` — wraps action, cancels after timeout, passes through on success
- `cache.wrap` — cache miss runs action + stores, cache hit skips action + returns cached, respects sliding
- `error.handle` — retry logic, goal-first vs retry-first, ignore, error filtering by status/key/message
- `Action.RunAsync` with modifiers — verify fold calls modifiers in correct order
- `Action.RunAsync` without modifiers — verify existing behavior unchanged (regression)
- Modifier handler resolution — `GetCodeGenerated` + `IModifier` cast succeeds
- Non-modifier cast to `IModifier` — verify clean error message

**PLang .goal tests:**
- Step with `cache for 5 min` on a file.read — verify caching works end-to-end
- Step with `on error retry 3 times` — verify retry happens
- Step with `timeout 2 sec` on a slow action — verify timeout fires
- Step with multiple modifiers on one action — verify composition
- Step with error handler on first action but not second — verify per-action scope
- Backward compat: existing .pr file with step-level `onError` still works

---

## Phase 2: Builder — Flat Output to Structured .pr

**Goal:** The builder produces .pr files with modifiers grouped onto actions. The LLM outputs flat actions; deterministic C# restructures them.

### 2.1 Update Builder Prompt

**Modify:** `system/builder/llm/BuildGoal.llm`

**Remove** the entire "Step Modifiers" section (lines 50-62).

The modifier modules (`error.handle`, `cache.wrap`, `timeout.after`) will appear in `{{ actionSummary }}` automatically via `Modules.Describe()`. The LLM picks them like any other action.

**Update the example** (lines 64-125) to show modifiers as flat actions in the actions array:

```json
{
  "index": 1,
  "guidance": "file.read with cache modifier. cache.wrap wraps the read.",
  "actions": [
    {"module": "file", "action": "read", "parameters": [{"name": "Path", "value": "config.json", "type": "path"}]},
    {"module": "cache", "action": "wrap", "parameters": [{"name": "DurationMs", "value": 300000, "type": "long"}]},
    {"module": "variable", "action": "set", "parameters": [{"name": "Name", "value": "%config%"}, {"name": "Value", "value": "%__data__%"}]}
  ],
  "level": "high", "confidence": 95
}
```

**Add a rule:**
```
- Modifier actions (error.handle, cache.wrap, timeout.after) always appear AFTER the action they modify.
  Multiple modifiers on the same action appear consecutively. The builder groups them automatically.
```

**Remove** the rule: `- on error MUST produce onError on the step. Missing onError is a build-breaking bug.`

**Replace** with: `- "on error" MUST produce an error.handle modifier action after the action it applies to.`

### 2.2 LLM Response Schema Update

**Modify:** `system/builder/BuildGoal.goal` (line 23 — the LLM schema in the `llm.query` call)

Remove `cache` and `onError` from the step schema. Currently includes:

```
cache?: {durationMs: long, sliding?: bool, key?: string},
onError?: {ignoreError?: bool, goal?: {name: string}, retryCount?: int, retryOverMs?: int, order?: string, message?: string}
```

Delete both. They're now just actions in the `actions` array.

**Also update** the `LlmFixer` goal (line 50) — it has the same schema duplicated.

### 2.3 Modifier Grouping in Save Pipeline

**Modify:** `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs`

Add grouping in `GoalsSave()` (line 135), **before** serialization (line 158):

```csharp
public async Task<Data.@this> GoalsSave(goalsSave action)
{
    // ... existing description logic (lines 140-152) ...

    // NEW: Group modifiers onto their preceding actions
    var modules = action.Context.App.Modules;
    foreach (var step in goal.Steps)
    {
        GroupModifiers(step.Actions, modules);
    }

    // ... existing serialization and save logic (lines 154+) ...
}

private static void GroupModifiers(Actions actions, App.Modules.@this modules)
{
    var flat = actions.ToList();
    actions.Clear();

    Action? current = null;
    foreach (var action in flat)
    {
        if (modules.IsModifier(action.Module, action.ActionName))
        {
            current?.Modifiers.Add(action);
        }
        else
        {
            current = action;
            actions.Add(current);
        }
    }

    // Sort each action's modifiers by order (outermost first)
    foreach (var action in actions)
    {
        if (action.Modifiers.Count > 1)
        {
            action.Modifiers.Sort((a, b) =>
                modules.GetModifierOrder(a.Module, a.ActionName)
                - modules.GetModifierOrder(b.Module, b.ActionName));
        }
    }
}
```

### 2.4 Update Step.Merge()

**Modify:** `PLang/App/Goals/Goal/Steps/Step/this.cs` — `Merge()` method (lines 292-317)

Remove the special cache/onError merging (lines 300-304):

```csharp
// REMOVE these lines:
// if (from.Cache != null)
//     Cache = from.Cache;
// if (from.OnError != null)
//     OnError = from.OnError;
```

Cache and error are now just actions in `from.Actions`.

### 2.5 Validation

Modifier actions pass validation like any other action. `DefaultBuilderProvider.Validate()` (line 177) calls `modules.Contains(a.Module, a.ActionName)` — modifier modules are registered via `[Action]`, so they validate automatically. Verify this works.

### Phase 2 Tests

**C# tests:**
- `GroupModifiers()` — flat list with no modifiers → unchanged
- `GroupModifiers()` — flat list with modifiers → grouped correctly onto preceding action
- `GroupModifiers()` — multiple modifiers on one action → sorted by order
- `GroupModifiers()` — modifier without preceding action → edge case handling
- `GroupModifiers()` — modifier between two executable actions → attaches to the one before it

**PLang .goal tests (golden eval cases):**
- `- read file.txt, cache for 5 min` → build → verify .pr has `cache.wrap` in `modifiers` on `file.read`
- `- call Save, on error retry 2 times, then call HandleError` → build → verify .pr has `error.handle` in `modifiers` on `goal.call`
- `- read file.txt, on 404 call FixFile, write to %content%, cache for 10 min` → build → verify per-action grouping
- `- read file.txt, timeout 5 sec, on error ignore` → build → verify ordering in modifiers

---

## Phase 3: Migration — Old .pr Files

**Goal:** Old .pr files with step-level `onError`/`cache`/`timeout` are automatically migrated to action-level modifiers on load.

### 3.1 Migration in Deserialization

When loading a .pr file, if step-level properties are present, convert them to modifier actions on the **first action** in the step.

**Where:** In the goal loading path — wherever .pr JSON is deserialized into Step objects. The coder must find this entry point.

### 3.2 Remove Step-Level Properties

After migration is in place:

**Modify:** `PLang/App/Goals/Goal/Steps/Step/this.cs` — remove `OnError`, `Cache`, `Timeout` properties and `RunActionsWithTimeout()`, `HandleErrorAsync()`, `Retry()`, `CallErrorGoal()` methods.

**Deprecate:** `cache.check`, `cache.store`, `error.check` — replaced by `cache.wrap` and `error.handle` modifiers.

**Caveat:** `CacheSettings` is used by `Cache.SetAsync()`. Don't delete until cache infra is updated.

### Phase 3 Tests

- Load old .pr → verify migration to modifiers on first action
- Load new .pr → no migration needed, works as-is
- Rebuild old .goal → produces new format

---

## Phase 4: New Modifiers

- `async.fire` (Order = 0) — fire-and-forget
- `parallel.run` (Order = 4) — concurrent iteration for foreach

Design details deferred to implementation.

---

## File Change Summary

### New Files
| File | Phase | Purpose |
|------|-------|---------|
| `PLang/App/modules/IModifier.cs` | 1 | Interface: `Wrap(next, context)` |
| `PLang/App/modules/ModifierAttribute.cs` | 1 | Attribute: `[Modifier(Order = N)]` |
| `PLang/App/modules/timeout/after.cs` | 1 | Timeout modifier handler |
| `PLang/App/modules/cache/wrap.cs` | 1 | Cache modifier handler |
| `PLang/App/modules/error/handle.cs` | 1 | Error modifier handler |
| `PLang/App/modules/async/fire.cs` | 4 | Async/fire-and-forget modifier |
| `PLang/App/modules/parallel/run.cs` | 4 | Parallel iteration modifier |

### Modified Files
| File | Phase | Change |
|------|-------|--------|
| `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` | 1 | Add `Modifiers` property, update `RunAsync` fold |
| `PLang/App/Modules/this.cs` | 1 | Add `IsModifier()`, `GetModifierOrder()`, update `Describe()` |
| `PLang/App/Goals/Goal/Steps/Step/this.cs` | 1 | Update `Clone()`. Phase 3: remove legacy properties |
| `system/builder/llm/BuildGoal.llm` | 2 | Remove "Step Modifiers" section, update examples/rules |
| `system/builder/BuildGoal.goal` | 2 | Remove cache/onError from schema (lines 23, 50) |
| `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` | 2 | Add `GroupModifiers()` in `GoalsSave()` |

### Deprecated (Phase 3)
| File | Reason |
|------|--------|
| `PLang/App/Goals/Goal/Steps/Step/ErrorHandler.cs` | Replaced by `error.handle` parameters |
| `PLang/App/Goals/Goal/Steps/Step/CacheSettings.cs` | Keep until cache infra updated |
| `PLang/App/modules/cache/check.cs` | Replaced by `cache.wrap` modifier |
| `PLang/App/modules/cache/store.cs` | Replaced by `cache.wrap` modifier |
| `PLang/App/modules/error/check.cs` | Replaced by `error.handle` modifier |

---

## Coder Checklist

Before implementing, the coder **must** verify these by reading code:

- [ ] `App.RunGoalAsync()` signature — used in `error.handle`. See `PLang/App/this.cs`
- [ ] `GoalCall` class — see `PLang/App/modules/error/check.cs:130` for existing usage
- [ ] `context.PushCancellation` / `PopCancellation` — see `Step/this.cs:154`
- [ ] `context.App.Cache` interface — see `cache/check.cs:31` and `cache/store.cs:34`
- [ ] `Modules.GetCodeGenerated()` — verify handler cast to `IModifier` works
- [ ] `Modules.GetActionType()` — verify exists, see `DefaultBuilderProvider.cs:230`
- [ ] `[Action]` on modifiers — verify source generator discovers and generates `ICodeGenerated`
- [ ] Existing action names: `cache/` has `check`, `store`; `error/` has `check`, `throw` — new names `wrap` and `handle` avoid conflicts
- [ ] Source generator + `IModifier` — verify no interference with `LazyParamsGenerator`
- [ ] `[Store]` on `Modifiers` — verify nested actions serialize correctly in .pr JSON
- [ ] `error.check` retry logic (lines 79-105) vs new `error.handle.Retry()` — new wraps delegate, old re-runs `Step.Actions`
