# Action Modifiers — Implementation Plan (Phases 1+2)

## Context

The branch `runtime2-action-modifiers` carries a design from the architect (`.bot/runtime2-action-modifiers/architect/v1/`) and a test suite from test-designer (`.bot/runtime2-action-modifiers/test-designer/v1/` — already in `PLang.Tests/App/Modules/modifier/` and `tests/modifiers/`).

**Problem it solves:** Today `OnError`, `Cache`, `Timeout` are step-level special cases touched by five layers (Step model, builder prompt, LLM schema, Step.Merge, Step.RunAsync). Adding a new modifier (e.g., `async`, `parallel`) means patching every layer. They also apply to the whole step, so caching a `file.read` also wraps the trailing `variable.set` for no reason.

**Target:** Modifiers become regular `module.action` records with a `[Modifier(Order=N)]` attribute, living in a `Modifiers` array on each `Action`. The LLM emits a flat list; the builder groups deterministically on save; the runtime folds them right-to-left via `IModifier.Wrap()`.

This plan covers Phases 1 (runtime) and 2 (builder) from `architect/v1/roadmap.md`. Phase 3 is not needed — **Ingi will rebuild all .pr files after this lands**, so there is no backward compat to preserve. Phase 4 (async/parallel modifiers) is deferred.

## Scope

48 tests to make pass: 42 C# in `PLang.Tests/App/Modules/modifier/*.cs` and 6 PLang in `tests/modifiers/*.test.goal`. All test bodies are `Assert.Fail`-style placeholders — the coder writes the assertions.

## Phase 1 — Runtime Infrastructure

### 1.1 New interface `IModifier`

**Create** `PLang/App/modules/IModifier.cs`:

```csharp
namespace App.modules;

public interface IModifier
{
    Func<Task<Data.@this>> Wrap(Func<Task<Data.@this>> next, Actor.Context.@this context);
}
```

### 1.2 New attribute `[Modifier]`

**Create** `PLang/App/modules/ModifierAttribute.cs`:

```csharp
namespace App.modules;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ModifierAttribute : Attribute
{
    public int Order { get; init; }
}
```

### 1.3 New smart collection `Modifiers.@this` and property on Action

Per OBP: "Collections are smart wrappers — Collection types (Steps, Actions) inherit List<T> and own domain operations." Modifiers is a collection with domain operations (the fold, the grouping) — it must own them, not the caller.

**Create** `PLang/App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs` — mirrors the existing `Actions.@this` pattern at `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs`:

```csharp
using System.Collections;

namespace App.Goals.Goal.Steps.Step.Actions.Action.Modifiers;

/// <summary>
/// Ordered list of modifier actions attached to an Action.
/// Owns the right-to-left fold that wraps an inner operation at runtime,
/// and the Order-based sort used by the builder.
/// </summary>
public sealed class @this : IList<Action.@this>
{
    private readonly List<Action.@this> _items = new();

    public @this() { }
    public @this(IEnumerable<Action.@this> items) { _items = new List<Action.@this>(items); }

    public Action.@this this[int index] { get => _items[index]; set => _items[index] = value; }
    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(Action.@this item) => _items.Add(item);
    public void AddRange(IEnumerable<Action.@this> items) => _items.AddRange(items);
    public void Clear() => _items.Clear();
    public bool Contains(Action.@this item) => _items.Contains(item);
    public void CopyTo(Action.@this[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public int IndexOf(Action.@this item) => _items.IndexOf(item);
    public void Insert(int index, Action.@this item) => _items.Insert(index, item);
    public bool Remove(Action.@this item) => _items.Remove(item);
    public void RemoveAt(int index) => _items.RemoveAt(index);
    public IEnumerator<Action.@this> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    /// <summary>
    /// Runs an inner operation wrapped by this collection right-to-left
    /// (first in list = outermost). Each Action resolves and initializes its own
    /// handler; this collection owns the iteration order.
    /// </summary>
    public async Task<Data.@this> RunAsync(
        Func<Task<Data.@this>> innermost,
        Actor.Context.@this context)
    {
        if (_items.Count == 0) return await innermost();

        var execute = innermost;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            var (wrapped, error) = await _items[i].WrapAround(execute, context);
            if (error != null) return Data.@this.FromError(error);
            execute = wrapped!;
        }
        return await execute();
    }
}
```

**Global alias** — add to `PLang/App/GlobalUsings.cs` and `PLang.Tests/GlobalUsings.cs`:

```csharp
global using ActionModifiers = App.Goals.Goal.Steps.Step.Actions.Action.Modifiers.@this;
```

**Property on Action** — in `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` after `Defaults` (line 41):

```csharp
[Store, Debug, Default]
public Modifiers.@this Modifiers { get; init; } = new();
```

The test `Action_Modifiers_DefaultsToEmptyList` only checks `.Count == 0` and `.IsNotNull()` — still passes with the new type.

### 1.4 `Action.WrapAround` + `Action.RunAsync` delegates to the collection

OBP: the Action owns its own handler resolution and initialization. Add a new instance method on `Action.@this`:

```csharp
/// <summary>
/// Returns a delegate that executes this modifier action around the given inner delegate.
/// Resolves the handler, verifies it implements IModifier, and runs ExecuteAsync so the
/// source-generated properties (DurationMs, Ms, etc.) are populated before Wrap() reads them.
/// </summary>
public async Task<(Func<Task<Data.@this>>? Wrapped, Errors.IError? Error)> WrapAround(
    Func<Task<Data.@this>> next,
    Actor.Context.@this context)
{
    var (handler, error) = context.App!.Modules.GetCodeGenerated(this);
    if (error != null) return (null, error);
    if (handler is not modules.IModifier mod)
        return (null, new Errors.ActionError(
            $"{Module}.{ActionName} is not a modifier", "ModifierError", 400));

    // Initialize: ExecuteAsync populates the generated properties. Run() returns Ok().
    await handler.ExecuteAsync(this, context);

    return (mod.Wrap(next, context), null);
}
```

Then `Action.RunAsync` simplifies — the action describes the shape (dispatch is innermost, modifiers wrap it) and delegates the fold:

```csharp
public async Task<Data.@this> RunAsync(Actor.Context.@this context)
{
    var lifecycle = context.LifecycleFor(this);

    var beforeResult = await lifecycle.Before.Run(context, App.Events.EventType.BeforeAction);
    if (!beforeResult.Success) return beforeResult;
    if (beforeResult.Handled) return beforeResult;

    Func<Task<Data.@this>> dispatch = () => context.App!.Run(this, context);
    var result = await Modifiers.RunAsync(dispatch, context);

    if (result.Success)
    {
        result.Name = "__data__";
        context.Variables.Put(result);
    }

    var afterResult = await lifecycle.After.Run(context, App.Events.EventType.AfterAction);
    if (!afterResult.Success) return afterResult;

    return result;
}
```

No external iteration. No registry lookup leaked out. No `handler is not IModifier` check in the caller. Each layer owns its piece:
- `Action` → resolves/validates/initializes its own handler; knows how to wrap one step
- `Modifiers.@this` → orchestrates the fold
- `Action.RunAsync` → describes innermost dispatch, delegates to Modifiers

**Note on property population:** `ExecuteAsync(this, context)` on the generated dispatcher (from `LazyParamsGenerator`) resolves parameters and assigns them to the partial properties. The same handler instance is then used for `Wrap()` — relies on per-call instantiation in `Modules.GetCodeGenerated` (confirmed in `App/Modules/this.cs:79-90`).

### 1.5 Clone modifiers in `Step.Clone()`

**Modify** `PLang/App/Goals/Goal/Steps/Step/this.cs` lines 268–276:

```csharp
Actions = new Actions.@this(Actions.Select(a => new Action
{
    Module = a.Module,
    ActionName = a.ActionName,
    Parameters = new List<Data.@this>(a.Parameters),
    Defaults = a.Defaults != null ? new List<Data.@this>(a.Defaults) : null,
    Errors = new List<Info>(a.Errors),
    Warnings = new List<Info>(a.Warnings),
    Modifiers = new ActionModifiers(a.Modifiers.Select(m => new Action
    {
        Module = m.Module,
        ActionName = m.ActionName,
        Parameters = new List<Data.@this>(m.Parameters)
    }))
})),
```

**Test fix:** `PLang.Tests/App/Modules/modifier/ModifierRegistryTests.cs:61` currently uses `Modifiers = new List<PrAction> { ... }`. Change it to `Modifiers = new ActionModifiers { ... }`. Assertions (`.Count`, `.IsNotSameReferenceAs`) still work since `ActionModifiers : IList<Action>`.

### 1.6 Registry — `IsModifier` / `GetModifierOrder`

**Modify** `PLang/App/Modules/this.cs` (after `IsCacheable` line 125):

```csharp
public bool IsModifier(string module, string actionName)
{
    var type = GetActionType(module, actionName);
    return type?.GetCustomAttribute<modules.ModifierAttribute>() != null;
}

public int GetModifierOrder(string module, string actionName)
{
    var type = GetActionType(module, actionName);
    var attr = type?.GetCustomAttribute<modules.ModifierAttribute>();
    return attr?.Order ?? int.MaxValue;
}
```

`Describe()` does not need a behavioral change — modifier actions appear automatically via `[Action]` registration. Leave it alone unless the `Describe_ModifierActions_AppearInSummary` test requires a marker (add only if assertions demand it).

### 1.7 Three modifier handlers

All three follow the existing partial-class + source-generator pattern. They implement `IModifier` alongside `IContext`. `Run()` returns `Ok()` — real work is in `Wrap()`.

**Create** `PLang/App/modules/timeout/after.cs`:

```csharp
namespace App.modules.timeout;

[Action("after", Cacheable = false)]
[Modifier(Order = 1)]
public partial class After : IContext, IModifier
{
    [IsNotNull]
    public partial Data.@this<int> Ms { get; init; }

    public Task<Data.@this> Run() => Task.FromResult(Data.@this.Ok());

    public Func<Task<Data.@this>> Wrap(Func<Task<Data.@this>> next, Actor.Context.@this context)
    {
        var ms = Ms.Value;
        return async () =>
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            cts.CancelAfter(ms);
            context.PushCancellation(cts);
            try { return await next(); }
            catch (OperationCanceledException) when (cts.IsCancellationRequested
                && !context.CancellationToken.IsCancellationRequested)
            {
                return Data.@this.FromError(new Errors.ServiceError(
                    $"Timed out after {ms}ms", "Timeout", 408));
            }
            finally { context.PopCancellation(); }
        };
    }
}
```

**Create** `PLang/App/modules/cache/wrap.cs`:

```csharp
namespace App.modules.cache;

[Action("wrap", Cacheable = false)]
[Modifier(Order = 2)]
public partial class Wrap : IContext, IModifier
{
    [IsNotNull]
    public partial Data.@this<long> DurationMs { get; init; }
    [Default(false)]
    public partial Data.@this<bool> Sliding { get; init; }
    public partial Data.@this<string>? Key { get; init; }

    public Task<Data.@this> Run() => Task.FromResult(Data.@this.Ok());

    public Func<Task<Data.@this>> Wrap(Func<Task<Data.@this>> next, Actor.Context.@this context)
    {
        var key = !string.IsNullOrEmpty(Key?.Value) ? Key.Value : DefaultKey(context);
        var durationMs = DurationMs.Value;
        var sliding = Sliding.Value;

        return async () =>
        {
            var cache = context.App!.Cache;
            var cached = await cache.GetAsync(key);
            if (cached != null)
            {
                cached.Name = "__data__";
                context.Variables.Put(cached);
                return cached;
            }
            var result = await next();
            if (result.Success)
                await cache.SetAsync(key, result, new CacheSettings { DurationMs = durationMs, Sliding = sliding });
            return result;
        };
    }

    private string DefaultKey(Actor.Context.@this context)
    {
        var step = context.Step;
        return $"step:{step?.Goal?.Path ?? "unknown"}:{step?.Index}";
    }
}
```

**Create** `PLang/App/modules/error/handle.cs`:

Structure mirrors `error/check.cs:79-132` for retry+goal logic, but wraps the `next` delegate instead of re-running `Step.Actions`. Properties: `StatusCode?`, `Key?`, `Message?`, `Goal? (GoalCall)`, `RetryCount?`, `RetryOverMs?`, `Order? (ErrorOrder)`, `IgnoreError`. Reuse existing `ErrorOrder` enum.

```csharp
namespace App.modules.error;

[Action("handle", Cacheable = false)]
[Modifier(Order = 3)]
public partial class Handle : IContext, IModifier
{
    public partial Data.@this<int>? StatusCode { get; init; }
    public partial Data.@this<string>? Key { get; init; }
    public partial Data.@this<string>? Message { get; init; }
    public partial Data.@this<GoalCall>? Goal { get; init; }
    public partial Data.@this<int>? RetryCount { get; init; }
    public partial Data.@this<int>? RetryOverMs { get; init; }
    public partial Data.@this<ErrorOrder>? Order { get; init; }
    [Default(false)]
    public partial Data.@this<bool> IgnoreError { get; init; }

    public Task<Data.@this> Run() => Task.FromResult(Data.@this.Ok());

    public Func<Task<Data.@this>> Wrap(Func<Task<Data.@this>> next, Actor.Context.@this context)
    {
        return async () =>
        {
            var result = await next();
            if (result.Success) return result;
            if (!MatchesError(result.Error)) return result;
            if (IgnoreError.Value) return Data.@this.Ok();

            var order = Order?.Value ?? ErrorOrder.RetryFirst;
            if (order == ErrorOrder.GoalFirst)
            {
                if (Goal?.Value != null)
                {
                    var goalResult = await CallErrorGoal(result, context);
                    if (goalResult.Success) return goalResult;
                }
                var retry = await Retry(next, context);
                if (retry?.Success == true) return retry;
                if (Goal?.Value != null) return Data.@this.Ok();
            }
            else
            {
                var retry = await Retry(next, context);
                if (retry?.Success == true) return retry;
                if (Goal?.Value != null) { await CallErrorGoal(result, context); return Data.@this.Ok(); }
            }
            return result;
        };
    }
    // MatchesError, Retry, CallErrorGoal — mirror error/check.cs:79-154
}
```

### 1.8 Remove legacy step-level modifier handling (no backward compat)

Per Ingi's decision: rebuild .pr files after this lands, so no migration needed.

**Modify** `PLang/App/Goals/Goal/Steps/Step/this.cs`:
- Delete properties `OnError` (line 83), `Cache` (line 86), `Timeout` (line 89)
- Simplify `RunAsync` (lines 115–148) — remove try/catch/timeout wrapping and `HandleErrorAsync` call; becomes `before events → RunActions → after events`
- Delete methods: `RunActionsWithTimeout` (150), `HandleErrorAsync` (182), `Retry` (217), `CallErrorGoal` (236)
- Update `Clone()` (260) — remove `OnError = OnError, Cache = Cache, Timeout = Timeout` lines

**Delete files:**
- `PLang/App/Goals/Goal/Steps/Step/ErrorHandler.cs` — replaced by `error.handle` parameters
- `PLang/App/modules/cache/check.cs` — replaced by `cache.wrap`
- `PLang/App/modules/cache/store.cs` — replaced by `cache.wrap`
- `PLang/App/modules/error/check.cs` — replaced by `error.handle`

**Keep:**
- `ErrorOrder` enum (in `ErrorHandler.cs`) — move it to its own file `PLang/App/Goals/Goal/Steps/Step/ErrorOrder.cs` since `error.handle` still needs it. Also still referenced by `global using ErrorOrder = ...` in `PLang.Tests/GlobalUsings.cs`.
- `CacheSettings.cs` — still required by the `ICache.SetAsync(key, value, CacheSettings)` signature; `cache.wrap` constructs one to pass in.
- `error/throw.cs` — unrelated, stays

**Remove dead code** in `PLang/App/Modules/this.cs`:
- Check whether `IsCacheable` (lines 119–125) is still used after `cache/check.cs`/`cache/store.cs` are deleted — if not, remove it too. It's currently called only from those two files.

### Phase 1 verification

Run `dotnet run --project PLang.Tests` — all 42 C# modifier tests should pass. Legacy tests that relied on `Step.OnError`/`Cache`/`Timeout` will need deletion; grep `PLang.Tests/` for usages. Test-designer's suite already replaces the coverage.

## Phase 2 — Builder

### 2.1 Builder prompt

**Modify** `system/builder/llm/BuildGoal.llm`:

- Delete lines 50–62 (the entire "## Step Modifiers" section)
- Remove the rule on line 178: `on error MUST produce onError on the step...`
- Add a new rule: `"on error", "cache for", "timeout" produce modifier actions (error.handle, cache.wrap, timeout.after) AFTER the action they modify. The builder groups them automatically.`
- Update examples (lines 89–123) to emit `cache.wrap` and `error.handle` as regular actions in the `actions` array instead of `cache`/`onError` step-level JSON.

### 2.2 LLM schema

**Modify** `system/builder/BuildGoal.goal` lines 23 and 50 — delete `cache?: {...}` and `onError?: {...}` from both schemas.

### 2.3 `Actions.GroupModifiers(modules)` — smart collection owns the grouping

OBP: `Actions` is already a smart collection (`Actions.@this : IList<Action.@this>` at `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs`). It should own the grouping operation, not the builder.

**Modify** `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs` — add method:

```csharp
/// <summary>
/// Takes a flat list where modifier actions (those with [Modifier]) follow their target,
/// and groups each modifier onto the preceding executable action's Modifiers collection.
/// Modifiers are then sorted by their [Modifier(Order = N)] so the outermost wrapper comes first.
/// A leading modifier with no preceding executable is dropped.
/// Mutates this collection in place.
/// </summary>
public void GroupModifiers(App.Modules.@this modules)
{
    if (_items.Count == 0) return;

    var flat = _items.ToList();
    _items.Clear();
    Action.@this? current = null;

    foreach (var action in flat)
    {
        if (modules.IsModifier(action.Module, action.ActionName))
        {
            current?.Modifiers.Add(action);
        }
        else
        {
            current = action;
            _items.Add(action);
        }
    }

    foreach (var action in _items)
    {
        if (action.Modifiers.Count <= 1) continue;
        var sorted = action.Modifiers
            .OrderBy(m => modules.GetModifierOrder(m.Module, m.ActionName))
            .ToList();
        action.Modifiers.Clear();
        foreach (var m in sorted) action.Modifiers.Add(m);
    }
}
```

**Modify** `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — in `GoalsSave` (line 135), before serialization (line 158):

```csharp
foreach (var step in goal.Steps)
    step.Actions.GroupModifiers(app.Modules);
```

One line. The builder stays thin; `Actions` owns its own restructuring. The `GroupModifiersTests.cs` batch tests exercise `Actions.GroupModifiers()` directly.

### 2.4 `Step.Merge` cleanup

**Modify** `PLang/App/Goals/Goal/Steps/Step/this.cs` lines 300–304 — delete `Cache` and `OnError` special-case merging. Already covered by §1.8 property removal, but noted here because it's part of the builder pipeline.

### Phase 2 verification

1. `plang p build` the `tests/modifiers/` test goals
2. Read each generated `.pr` file and verify `modifiers` array appears on the intended action with correct ordering
3. `plang --test` — all 6 PLang test goals should pass
4. Rebuild an existing non-modifier goal and confirm its `.pr` is unchanged (regression)

## Critical files

**New:**
- `PLang/App/modules/IModifier.cs`
- `PLang/App/modules/ModifierAttribute.cs`
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs` — smart collection with `RunAsync(innermost, context)`
- `PLang/App/modules/timeout/after.cs`
- `PLang/App/modules/cache/wrap.cs`
- `PLang/App/modules/error/handle.cs`
- `PLang/App/modules/timer/sleep.cs` (for PLang timeout test)

**Modified:**
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — `Modifiers` property (typed `Modifiers.@this`) + new `WrapAround()` method + simplified `RunAsync` delegating to `Modifiers.RunAsync`
- `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs` — new `GroupModifiers(modules)` method (smart collection owns grouping)
- `PLang/App/Goals/Goal/Steps/Step/this.cs` — `Clone()` copies modifiers; `Merge()` drops special cache/OnError handling
- `PLang/App/Modules/this.cs` — `IsModifier()`, `GetModifierOrder()`
- `PLang/App/GlobalUsings.cs` + `PLang.Tests/GlobalUsings.cs` — add `ActionModifiers` alias
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — call `step.Actions.GroupModifiers(app.Modules)` in `GoalsSave`
- `system/builder/llm/BuildGoal.llm` — remove Step Modifiers section, update examples/rules
- `system/builder/BuildGoal.goal` — remove cache/onError from schema (lines 23, 50)
- `PLang.Tests/App/Modules/modifier/ModifierRegistryTests.cs:61` — `new List<PrAction>` → `new ActionModifiers`

**Deleted:**
- `PLang/App/Goals/Goal/Steps/Step/ErrorHandler.cs` (keep `ErrorOrder` enum by extracting to `ErrorOrder.cs`)
- `PLang/App/modules/cache/check.cs`
- `PLang/App/modules/cache/store.cs`
- `PLang/App/modules/error/check.cs`

**Kept:**
- `PLang/App/Goals/Goal/Steps/Step/CacheSettings.cs` — still needed by `ICache.SetAsync` signature
- `PLang/App/Goals/Goal/Steps/Step/ErrorOrder.cs` (extracted) — used by `error.handle`

**New PLang action for timeout test:**
- `PLang/App/modules/timer/sleep.cs` — `[Action("sleep")] public partial class Sleep : IContext { partial int Ms; Run() => await Task.Delay(Ms, context.CancellationToken); }`. Needed so `tests/modifiers/TimeoutOnSlowAction.test.goal` can simulate a slow action. Rewrite that .goal to: `- call Sleep ms=5000, timeout after 100ms, on error ignore` then assert the timeout fired.

## Decisions (from Ingi)

- **Timeout test mechanism:** add a new `timer.sleep` action (minimal handler using `Task.Delay`). Used by `TimeoutOnSlowAction.test.goal`.
- **`Order` parameter on `error.handle`:** typed as the existing `ErrorOrder` enum (not string).
- **Backward compat:** none. Ingi rebuilds all .pr files after this lands. Legacy `Step.OnError`/`Cache`/`Timeout` and their handling are fully deleted.

## Verification (end-to-end)

1. `dotnet run --project PLang.Tests` — 42 C# modifier tests pass. Any test files that reference `Step.OnError`/`Cache`/`Timeout` need deletion (grep first — test-designer's suite covers the new behavior).
2. `plang p build` from project root — builder rebuilds **all** .pr files with the new modifier structure. Delete `.build` folders where necessary so the builder starts fresh.
3. Read a handful of generated .pr files to verify the `modifiers` array shape (modifier grouped onto preceding action, sorted by `Order`).
4. `plang --test` — 6 PLang modifier tests pass.
5. Run one existing non-modifier PLang goal to confirm the refactor didn't break unrelated paths.
