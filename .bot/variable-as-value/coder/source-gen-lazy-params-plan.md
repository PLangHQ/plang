# Source-gen: lazy params — assign the Data, resolve in the handler

**Branch:** off `variable-as-value`. **Status:** design agreed with Ingi; ready to implement.

## Problem
The dispatch eagerly resolves each param into a backing field
(`__X_backing = ShallowClone<T>(await __d.Value<T>())`). Two costs:
1. The resolution error fires **before `Run()`** (`__resolutionError`), so it's
   **unguardable** — `goal.call`'s null `Actor` errors at dispatch (`convert null → actor`,
   `catalog["actor"]` miss), and `call.cs`'s `?? Context` fallback never runs.
2. It's **redundant** — handlers already call `await X.Value()` in their bodies
   (`call.cs`, `OpenAi`), so the value is resolved twice.

The backing fields + `__set` flags + the reset block + `__ResolveData` exist **only** to
cache that eager resolve. With no dispatch resolve, all of it goes.

## Design (agreed)
- **`Data.As<T>()`** — typed view `Data → Data<T>`: shares `_type` / `Properties` /
  `Context` **by reference**, NO clone, NO resolve. It's `Value<T>()` minus the
  `await Value()` — just hands over the typed face. (`if (this is @this<T> t) return t;`)
- **Handler param properties become plain `{ get; set; }`** auto-properties — no backing
  field, no computed getter, no `__set` flag.
- **`ExecuteAsync` becomes `static`**: `new Handler()` per dispatch (no shared/reset
  instance). Wire `Context` (IContext), `__action` (self-ref: params + goal-call anchor),
  `__app` (`= Context.App`, feeds `[Code]` getters). Then loop `action.Parameters` once,
  `switch (p.Name)`, assign `handler.X = p.As<T>()` (T = the handler property's declared
  type). Then `try { return await handler.Run(); } catch { wrap module.action + __step/__callFrames }`.
- **`[Code]` provider getters stay lazy** (self-inject from `__app`).
- **`actor` becomes an `item`** (wrapped via `clr.cs`, the host-object carrier) so
  `Data<actor>` is a legit value carrier — no `catalog["actor"]` miss.
- **Fix the swallow** — a real `As<T>`/conversion failure surfaces with the module.action
  prefix, never null.

## Generated shape (goal.Call)
```csharp
public static async Task<Data> ExecuteAsync(action.@this action, context Context)
{
    var handler = new Call();
    handler.Context  = Context;          // IContext
    handler.__action = action;           // self-ref (params + navigation/anchor)
    handler.__app    = Context.App;      // source for [Code] getters
    foreach (var p in action.Parameters)
    {
        p.Context = Context;
        switch (p.Name.ToLowerInvariant())
        {
            case "goalname": handler.GoalName = p.As<global::app.goal.GoalCall>(); break;
            case "actor":    handler.Actor    = p.As<global::app.actor.@this>();   break;
        }
    }
    try { return await handler.Run(); }
    catch (Exception ex) when (...) { /* {module}.{action}: + __step/__callFrames */ }
}
// param surface is just:  public partial data.@this<GoalCall> GoalName { get; set; }
```

## Blast radius
- **Source gen**: Property emitter → plain `{ get; set; }`, drop `EmitDispatchResolve`.
  Action emitter → static `ExecuteAsync` (new + wire + loop + try/Run), drop
  `__ResolveData`, the reset block, `__ResolveParameters`.
- **Every handler**: the hand-declared `partial` param properties change `{ get; }` →
  `{ get; set; }` (so the dispatch can assign). Count + sweep.
- **Dispatcher** (`Modules` / wherever `ExecuteAsync` is invoked): call the static form
  `Handler.ExecuteAsync(action, context)` instead of a shared reset-between-runs instance.
- **Tests / `App.RunAction`**: direct composition `new Handler { Prop = ... }` works with
  `{ get; set; }`.
- **`actor` → `clr` item**: `actor.@this` becomes an item (clr carrier); confirm `Data<actor>` round-trips.

## Refined design (Ingi): object-initializer dispatch, no backing-cache

The dispatch should **construct the handler with its params** instead of `new()`-empty-then-fill:
```csharp
var h = new Call {
    Context  = context,                               // IContext
    GoalName = action["goalname"].As<GoalCall>(context),     // present → value; absent → NotFound→As<T> = uninitialized
    Cache    = action["cache"] is { IsInitialized: true } c  // [Default]: present → As<T>, absent → default literal
                 ? c.As<@bool>(context) : new data.@this<@bool>("cache", true),
};
// required ([IsNotNull]/non-null Data<Variable>): sync presence check → MissingRequiredParameter
return await h.Run();
```
**Enablers (DONE, committed):**
- `Data.As<T>(context)` — typed view stamped with the execution scope.
- `action["name"]` indexer (`action/this.cs`) — by-name param/Default/NotFound, so the dispatch reads `action["name"].As<T>(context)`.

**Properties** shrink to `get => __backing; init => __backing = value;` (drop `__X_set`, getter-fallback, reset) — the
initializer sets every slot. Handler `{ get; init; }` decls are UNCHANGED (init = object-initializer). Gen-only.

**The big interlocking piece — `Emission/Action/this.cs` `EmitExecuteAsync`** does far more than resolve params;
ALL of this must be preserved, re-pointed at the freshly-constructed `h`:
- `Context`/`Channel`(IChannel)/`Action`(IAction)/`Step`(IStep)/`Static`(IStatic) wiring
- eager `[Code]` provider resolution (sets the `[Code]` backing — those stay lazy-injected)
- `IEvent` surface (`context.Event = X.Event`)
- `[IsNotNull]` + `MissingRequiredParameter` validation (currently pre-`__ResolveParameters`)
- `try { Run() } catch` wrap + `__PrefixActionContext`
- **a SECOND dispatch path** (the Build/`SetAction` emit, ~line 327) + the **prebound-C#** path (`action == null`)
- **`__SnapshotParams`** (reads `__X_set`/backing — must move to reading the property)
- DELETE: `__ResolveData`, `__ResolveParameters`, the reset blocks, `__X_set` flags, getter-fallbacks.

Risk: regenerates EVERY handler — a slip breaks the whole build. Do it as a focused step with a clean machine,
`dotnet build` + one Hello build to verify.

## Open
- Failed `As<T>` (genuinely wrong type) keeps the `module.action` prefix the old
  `__PrefixActionContext` gave.
- Read-caching: a property read returns the assigned `As<T>` view; `.Value()` resolves
  each read. If repeated reads must share a resolve, cache on the param `Data`, not the
  handler. (Default: cache-free.)
