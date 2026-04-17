# v1 Summary — Action Modifiers Implementation

## What this is

Implementation of the action modifiers feature designed by the architect and spec'd by the test-designer. Legacy step-level `OnError`/`Cache`/`Timeout` properties (plus their handling code, the `cache.check`/`cache.store`/`error.check` handlers, and `ErrorHandler.cs`) are **fully removed** — Ingi confirmed no backward compat is needed; he will rebuild all `.pr` files after this lands. Phases 1 (runtime infrastructure) + 2 (builder pipeline) from `architect/v1/roadmap.md`. Phases 3 (migration) and 4 (async/parallel) are deferred.

## What was done

**Runtime infrastructure (Phase 1):**
- `PLang/App/modules/IModifier.cs` — the one contract: `Wrap(next, context)`
- `PLang/App/modules/ModifierAttribute.cs` — `[Modifier(Order = N)]`, lower Order = outermost wrapper
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/Modifiers/this.cs` — smart collection (`ActionModifiers : IList<Action>`) that **owns the fold**. `RunAsync(innermost, context)` walks the list right-to-left; each action resolves its own handler via `WrapAround`.
- `Action.WrapAround(next, context)` on `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — the action resolves its own handler, casts to `IModifier`, runs `ExecuteAsync` to populate source-generated properties, returns the wrapped delegate. No external registry leak.
- `Action.RunAsync` simplified — just calls `Modifiers.RunAsync(dispatch, context)`. No external iteration.
- `Modules.IsModifier()` / `GetModifierOrder()` — registry introspection.
- Three modifier handlers (`timeout/after.cs`, `cache/wrap.cs`, `error/handle.cs`), plus `timer/sleep.cs` for the PLang timeout integration test.

**Builder pipeline (Phase 2):**
- Builder prompt (`system/builder/llm/BuildGoal.llm`) — removed the "Step Modifiers" section; updated examples to emit modifier *actions* in the flat list; added a rule that modifier actions must follow their target.
- LLM schema (`system/builder/BuildGoal.goal`) — removed `cache` and `onError` step-level properties from the JSON scheme.
- `Actions.GroupModifiers(modules)` — new method on the smart collection (not a loose helper). Takes the flat LLM output, groups modifier actions onto the preceding executable action, sorts each group by `[Modifier(Order)]`. Leading modifiers are dropped.
- `DefaultBuilderProvider.GoalsSave` — one-line call: `step.Actions.GroupModifiers(app.Modules)` before serialization.

**Legacy cleanup (no backward compat):**
- Deleted `Step.OnError`/`Cache`/`Timeout` properties and all their handling (`RunActionsWithTimeout`, `HandleErrorAsync`, `Retry`, `CallErrorGoal`)
- Deleted `ErrorHandler.cs`, `cache/check.cs`, `cache/store.cs`, `error/check.cs`
- Extracted `ErrorOrder` enum to its own file (still used by `error.handle`)
- Kept `CacheSettings.cs` — needed by `ICache.SetAsync` signature

**Tests:**
- All 42 C# modifier tests written and passing
- 2104/2105 total C# suite passing — the single failure (`Query_ToolCall_LlmRequestsToolAndHandlesError` in LLM integration) is unrelated and fails on baseline too (snapshot/network test)
- `tests/modifiers/TimeoutOnSlowAction.test.goal` rewritten to use the new `timer.sleep` action

## Code example

The smart collection owns the fold — each action handles its own resolution. Before (OBP violation Ingi caught):

```csharp
// Action.RunAsync loops Modifiers, resolves handlers from the registry, casts to IModifier
// — external code decomposing another object's collection. Bad.
if (Modifiers.Count > 0)
{
    for (int i = Modifiers.Count - 1; i >= 0; i--)
    {
        var modifier = Modifiers[i];
        var (handler, error) = app.Modules.GetCodeGenerated(modifier);
        if (handler is not IModifier mod) return ...
        await handler.ExecuteAsync(modifier, context);
        execute = mod.Wrap(execute, context);
    }
}
```

After:

```csharp
// Action.RunAsync
Func<Task<Data>> dispatch = () => context.App!.Run(this, context);
var result = await Modifiers.RunAsync(dispatch, context);

// Modifiers.RunAsync
for (int i = Count - 1; i >= 0; i--)
{
    var (wrapped, error) = await this[i].WrapAround(execute, context);
    if (error != null) return Data.FromError(error);
    execute = wrapped!;
}
return await execute();

// Action.WrapAround — each action knows how to resolve and init its own handler
var (handler, error) = context.App!.Modules.GetCodeGenerated(this);
if (handler is not IModifier mod) return (null, ModifierError(...));
await handler.ExecuteAsync(this, context);
return (mod.Wrap(next, context), null);
```

Each layer owns its piece: `Action` resolves its own handler, `Modifiers` orchestrates the fold, `Action.RunAsync` describes the shape and delegates.

## Gotcha worth knowing

The generated `ExecuteAsync` wraps *all* exceptions (including `OperationCanceledException`) into `ServiceError` results. So `timeout.after.Wrap` can't rely on catching OCE from a timed-out inner action — it detects the timeout via `cts.IsCancellationRequested && !result.Success` after `await next()`. Parent cancellation is re-thrown explicitly so it propagates up past this modifier.

## Status

- Phase 1 + Phase 2 complete, passing tests, both projects build clean.
- PLang-side end-to-end verification (`plang p build` + `plang --test`) deferred — Ingi will rebuild all `.pr` files and run the PLang tests himself. The six `tests/modifiers/*.test.goal` files are ready with real step text.
- Next recommended agent: **codeanalyzer** for review.
