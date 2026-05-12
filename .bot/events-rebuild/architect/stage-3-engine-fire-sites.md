# Stage 3: Migrate engine fire sites

**Goal:** All engine fire sites call the new `ctx.event.Before/After(On.X, Data source)` surface. Today's `lifecycle.Before.Run(context, EventType.X, ...)` calls go away.

**Scope:**
- Migrate every `lifecycle.Before.Run(...)` / `lifecycle.After.Run(...)` call to the new shape.
- Keep the old `lifecycle.Before.Run` infrastructure callable (it's used by tests, MOCK fixtures, etc.) but route it internally to the new `ctx.event.Before` so it's a thin shim — Stage 5 deletes the shim and its folder.

**Out of scope:**
- Data.Value firing (Stage 4).
- Deleting the `Lifecycle/` folder (Stage 5).

**Deliverables:**
- Modified files (all under `PLang/App/`):
  - `Goals/Goal/this.cs` — `BeforeGoal` / `AfterGoal` fire via `ctx.event.Before(On.Goal, new Data<Goal>(this))`.
  - `Goals/Goal/Steps/Step/this.cs` — `BeforeStep` / `AfterStep`.
  - `Goals/Goal/Steps/Step/Actions/Action/this.cs` — `BeforeAction` / `AfterAction`. (Modifiers/this.cs:61 today.)
  - `Channels/Channel/this.cs` — `BeforeWrite` / `AfterWrite` / `BeforeRead` / `AfterRead` / `OnAsk`.
- Tests:
  - Existing PLang event tests pass unchanged. They assert "when I write `on before step`, my goal runs before each step." Behavior preserved.
  - C# parity tests: register on the new shape directly, run a flow, assert handler fired.

**Dependencies:** Stage 1 + Stage 2. (Bindings need a registry to write to, and `event.on` needs to write the new shape.)

## Design

### The migration pattern

Today in `Step/Actions/Action/this.cs:149`:

```csharp
var beforeResult = await lifecycle.Before.Run(context, App.Events.EventType.BeforeAction);
// ... run action ...
var afterResult  = await lifecycle.After.Run(context, App.Events.EventType.AfterAction, this, result);
```

After:

```csharp
await context.Event.Before(On.Action, new Data<Action>(this));
// ... run action ...
await context.Event.After(On.Action, new Data<Action>(this, properties: { ["result"] = result }));
```

The `Data<T>(this)` wrap is one allocation per fire site. With the fast-path mask, when no bindings are registered for `On.Action`, the call short-circuits before any matching work — the allocation is the only cost paid.

Acceptable; can be optimized later by passing `this` as `object` and wrapping inside `Event.@this` only when a binding actually matches. v1 keeps the API uniform (always `Data`).

### Channel fire sites

Channel methods today fire three categories:

```csharp
// Channel.Write (today):
var beforeAborted = await FireBefore(global::App.Events.EventType.BeforeWrite, data, null);
// ... write ...
await FireAfter(global::App.Events.EventType.AfterWrite, result, null);

// After Stage 3:
await Context.Event.Before(On.Write, new Data<Channel>(this, properties: { ["payload"] = data }));
// ... write ...
await Context.Event.After(On.Write, new Data<Channel>(this, properties: { ["result"] = result }));
```

`FireBefore` / `FireAfter` helpers on Channel.@this go away (or become thin wrappers that route to `Context.Event` — Stage 5 deletes them).

### What about `BeforeAppStart` / `AfterAppStart`?

Today's enum has these values, but **no call site fires them** (Thread 3, filed in `Documentation/Runtime2/todos.md`). Stage 3 does NOT add the fire sites — that's Thread 3's job (its own branch). After Stage 3, the App lifecycle events still don't fire. Same behavior as today, different shape.

When Thread 3 lands, it'll add:
```csharp
// App.Start():
await this.Event.Before(On.App, new Data<App>(this));
// ... boot ...
await this.Event.After(On.App, new Data<App>(this));
```

Trivial addition once this branch's foundation is in place.

### Handler abort semantics

Today: `lifecycle.Before.Run` returns a result; if Before-handlers signal abort, the calling step doesn't run. The current shape uses Data inspection on the return.

New shape: `ctx.event.Before(...)` returns `Task` (void-ish). To preserve abort semantics, either:
- **(I)** Return `Task<Data>` — fire site inspects the returned Data, aborts if `!Ok`.
- **(II)** Use a side channel: Before-handler that wants to abort sets a flag on `Context` (e.g., `Context.AbortRequested = true`); fire site checks the flag.

Lean **(I)**. Same shape as today, just renamed. Fire site:

```csharp
var beforeResult = await context.Event.Before(On.Action, new Data<Action>(this));
if (!beforeResult.Ok) return beforeResult;  // handler asked to abort
```

Update `Before`/`After` signature on `Event.@this`:

```csharp
public Task<Data> Before(On on, Data source);
public Task<Data> After(On on, Data source);
```

Default return is `Data.Ok()` when no handler fires or all handlers succeed. First handler returning a non-Ok Data aborts the chain and is returned.

### The Lifecycle shim

To keep tests green between Stage 3 (engine sites migrated) and Stage 5 (Lifecycle folder deleted):

```csharp
// PLang/App/Events/Lifecycle/this.cs (during migration window):
public partial class @this
{
    public Bindings Before { get; }
    public Bindings After { get; }
}

// Bindings.Run(context, EventType type) internally translates to:
public Task<Data> Run(Context ctx, EventType type, params object[] args)
{
    var (on, phase) = TypeToOnPhase(type);
    var source = ConstructSourceData(type, args);
    return phase == Phase.Before
        ? ctx.Event.Before(on, source)
        : ctx.Event.After(on, source);
}
```

This means any test/external code still calling `lifecycle.Before.Run(ctx, EventType.BeforeStep)` keeps working — it just routes through the new registry. Stage 5 deletes this shim plus the Lifecycle folder.

### What NOT to do in this stage

- Don't delete the old `Events.@this`, `Lifecycle.@this`, `Bindings.@this`, or `EventType.cs`. The shim keeps them callable. Stage 5 deletes.
- Don't touch `Data.Value`. Stage 4.
