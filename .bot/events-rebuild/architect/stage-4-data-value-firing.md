# Stage 4: Data.Value fires variable events

**Goal:** `Data.Value` getter and setter fire `On.Variable` events through the universal hook. Variable events become user-registrable end-to-end.

**Scope:**
- Modify `PLang/Runtime2/Engine/Memory/Data.cs` (or wherever `Data.@this` lives — see [data-value-firing.md](plan/data-value-firing.md)) to fire Before/After variable events on every `.Value` access.
- Ensure Data carries `Name` and `Path` so the registry can match.
- Update the variable resolver to stamp Name and Path when constructing Data from dot-path walks.
- Performance-validate: zero-binding case must remain ~free.

**Out of scope:**
- The builder catalog for `%step.Text%` validation (Stage 2 / deferred).
- Removing the old Events folder chain (Stage 5).

**Deliverables:**
- `PLang/Runtime2/Engine/Memory/Data.cs` (or `App/Data/this.cs` depending on where Data lives) — `Name` and `Path` fields, fire hooks in `.Value`.
- Variable resolver changes (wherever dot-path walks are implemented) — propagate Name, accumulate Path.
- `App.@this` exposes `event` to Data for the firing context (Data already has Context; resolves via `Context.Event`).
- Tests:
  - Register `{on:Variable, name:"step", path:"Text", type:Before}`, access `%step.Text%` in a step, assert handler fires exactly once Before-side.
  - After-side fires after value is resolved.
  - Multi-segment walk `%step.Action.Module%` fires bindings at each segment for matching path patterns.
  - Result/literal Data (Name="") doesn't fire.
  - Set events: register Set binding, write to `%step.Text%` via PLang `- set`, assert handler fires.
  - **Performance microbench:** Data.Value access with zero variable bindings registered, compared to a control measurement on main. Must be within 5%.

**Dependencies:** Stage 1 (registry exists), Stage 2 (event.on can register variable bindings). Independent of Stage 3.

## Design

### Data fields

Add to `Data.@this`:

```csharp
public string Name { get; init; } = "";   // "" sentinel means "no firing"
public string? Path { get; init; }        // null for whole-variable access
public Context? Context { get; init; }    // already exists per Ingi 2026-05-12
```

`Name` defaults to `""` so all the existing Data constructions (Ok(), Fail(), result wraps) inherit the "no firing" state by default. Only resolver-constructed Data gets Name set.

### .Value getter hook

```csharp
public T Value
{
    get
    {
        if (Name == "" || Context is null) return ResolveValue();
        FireBefore();
        var v = ResolveValue();
        FireAfter();
        return v;
    }
}

private void FireBefore()
{
    if (!Context.Event.HasAnyBindingsFor(On.Variable) &&
        !Context.App.Event.HasAnyBindingsFor(On.Variable))
        return;   // fast path
    // Sync-firing path — see "Sync vs async" below.
    Context.Event.BeforeSync(On.Variable, this);
}
```

The double-check (local + app fast path) covers the case where Variable bindings exist only at app scope.

### Sync vs async firing inside .Value

`.Value` is sync. Variable-event handlers may want to be async (they're typically PLang goals).

Options (also discussed in [data-value-firing.md](plan/data-value-firing.md)):
- **(I)** Variable-event handlers MUST be sync. Document.
- **(II)** Make `.Value` async — `ValueTask<T>`. Big ripple across the codebase.
- **(III)** Two surfaces: `.Value` (sync, no firing) and `.ValueAsync()` (async, fires). PLang variable resolution uses `ValueAsync`; C# direct property access uses `.Value` and doesn't fire (preserves "PLang-only firing" rule).

**Recommended (III).** Coder: confirm with Ingi at the start of Stage 4. If (III) is too much rework for v1, (I) is acceptable as a v1 constraint with a clear "Variable handlers run sync; if you need async, move logic to a step that fires lifecycle events instead."

### Variable resolver changes

The resolver constructs Data when walking dot paths. It already exists somewhere in the runtime (`Engine/Variable/...` likely). Find the construction site, add Name/Path propagation:

```csharp
// Resolving %step.Text%:
var rootData = Context.Variables["step"];                       // existing
var textValue = AccessMember(rootData.Value, "Text");           // existing
return new Data<object>(textValue,
    name: rootData.Name,                                        // propagate from root
    path: "Text",                                               // first segment after root
    context: Context);                                          // existing
```

For deeper walks, accumulate path: `path: rootData.Path == null ? "Text" : rootData.Path + ".Text"`.

### What happens when a Data is constructed by the variable resolver but never read

Bindings only fire on `.Value` access. If a `%step.Text%` is built but the result is discarded (filter, short-circuit), no event fires. Per [data-value-firing.md](plan/data-value-firing.md) — the hook in `.Value` is more semantically honest than firing at construction.

### Performance acceptance criteria

Microbench: a tight loop reading `Data.Value` 1M times, no bindings registered. Compare against main's `Data.Value` cost. Acceptance: within 5%.

If we miss: the fast-path check is likely paying for two hashset hits + an interface dispatch through Context. Mitigations:
- Inline `HasAnyBindingsFor` (currently `(_activeOnMask & (1 << (int)on)) != 0`, already inline-friendly).
- Cache the App-level reference on Context.Event so the double-check doesn't traverse through Context.App.
- If still over, consider making `_activeOnMask` reachable directly via a field on Data's Context for one-instruction lookup.

These are coder-side optimizations once the basic implementation is in. Don't pre-optimize.

### What NOT to do in this stage

- Don't change Data's API for Properties, Error, Success, Ok, Fail. They stay.
- Don't fire events for non-variable Data (action returns, literals). Name="" is the only filter that matters.
- Don't reach into the variable resolver to fire events. The hook lives in `.Value`. The resolver only stamps Name/Path.
