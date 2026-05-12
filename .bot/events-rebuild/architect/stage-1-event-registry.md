# Stage 1: New `Event.@this` registry + scope owners + binding shape

**Goal:** Build the new event registry as a parallel structure alongside the existing `Events.@this`. New shape works in isolation; nothing dispatches through it yet.

**Scope:**
- Create `PLang/App/Event/` folder with `this.cs`, `On.cs`, `Phase.cs`.
- Create `PLang/App/Actor/Context/Event/this.cs` (uses the same `Event.@this` type, lives at a different ownership location).
- Implement the binding record (private nested), registration API, fire-surface API, fast-path mask.
- Add `App.@this.Event` property (alongside the existing `App.@this.Events` for now — both coexist during migration).
- Add `Actor.Context.@this.Event` property similarly.

**Out of scope:**
- Wiring fire sites to call the new registry (Stage 3).
- `event.on` action targeting the new registry (Stage 2).
- Removing the old `Events.@this` and friends (Stage 5).

**Deliverables:**
- `PLang/App/Event/this.cs` — the registry class. Public surface per [registry-internals.md](plan/registry-internals.md).
- `PLang/App/Event/On.cs` — the `On` enum (8 values).
- `PLang/App/Event/Phase.cs` — `Phase { Before, After }`.
- `PLang/App/this.cs` — new `Event Event { get; }` property; initialized in constructor.
- `PLang/App/Actor/Context/this.cs` — new `Event Event { get; }` property; initialized in constructor.
- C# tests under `PLang.Tests/App/EventTests/`:
  - Register a binding, observe it appears in `GetBindings(On.X)`.
  - Fire `Before(On.X, source)`, observe matching handler runs.
  - Pattern matching: glob, regex, exact, null/wildcard.
  - Fast-path: `HasAnyBindingsFor(On.X)` false when nothing registered.
  - Two-tier walk: `Context.event.Before(...)` consults both Context and App.
  - Save/Restore round-trip.

**Dependencies:** None — net-new code in a new folder.

## Design

### Binding record (private nested)

```csharp
public sealed partial class @this  // PLang/App/Event/this.cs
{
    private sealed record Binding(
        string Id,
        On On,
        string? Name,
        string? Path,
        Phase Type,
        bool IsRegex,
        int Priority,
        Func<Data, Task> Handler);
}
```

No `Binding.@this` public type. No `Lifecycle/Bindings/Binding/` folder.

### Storage and fast path

Per [registry-internals.md](plan/registry-internals.md):

```csharp
private readonly Dictionary<(On on, string name), List<Binding>> _byKey = new();
private int _activeOnMask;     // bitmask over On values
private readonly object _writeLock = new();

public bool HasAnyBindingsFor(On on) => (_activeOnMask & (1 << (int)on)) != 0;
```

### Fire methods

```csharp
public async Task Before(On on, Data source)
{
    if (!HasAnyBindingsFor(on)) return;
    await FireMatching(on, source, Phase.Before);
}

public async Task After(On on, Data source)
{
    if (!HasAnyBindingsFor(on)) return;
    await FireMatching(on, source, Phase.After);
}

private async Task FireMatching(On on, Data source, Phase phase)
{
    var name = source.Name ?? "";
    var bindings = new List<Binding>();
    
    lock (_writeLock)
    {
        if (_byKey.TryGetValue((on, name), out var exact))
            bindings.AddRange(exact);
        if (name != "" && _byKey.TryGetValue((on, ""), out var wild))
            bindings.AddRange(wild);
    }
    
    foreach (var b in bindings.Where(b => b.Type == phase)
                              .Where(b => PathMatches(b.Path, source.Path))
                              .OrderByDescending(b => b.Priority))
    {
        await b.Handler(source);
    }
}
```

The snapshot-under-lock-then-iterate pattern keeps handler execution outside the lock (handlers may take time).

### Pattern compilation

At registration time, glob patterns compile to a `Regex` (or a custom match function). Regex patterns compile directly. The compiled matcher is stored on the binding alongside the original pattern string (for inspection/save/restore).

Or simpler v1: store the pattern string, recompile on each match. Cheap with bounded binding counts. Decision: defer optimization unless profiling shows it matters.

### Two-tier walk in Context.Event

Same class, but its instances at Context level know about App:

```csharp
public sealed partial class @this  // a partial that lives on the per-Context view
{
    // The Context-side instance has a reference to the App-side instance.
    private readonly @this? _appLevel;

    public async Task Before(On on, Data source)
    {
        await BeforeLocal(on, source);
        if (_appLevel is not null) await _appLevel.BeforeLocal(on, source);
    }
}
```

Wait — same class, different instances, but instances at Context level have a back-reference to the App instance. That's the cleanest way to keep one class while encoding the two-tier walk.

Alternative: make `Before`/`After` virtual or use composition. Coder: lean toward composition — `Context.event` is a thin wrapper that has its own bindings list + a reference to `App.event` and consults both. Two classes if needed (`Event.@this` for App-side, `Event.Context.@this` for Context-side with the upstream reference) — but try the one-class-with-back-ref first; it's simpler.

### How `App.@this.Event` and `Actor.Context.@this.Event` get wired

```csharp
// App constructor (PLang/App/this.cs):
this.Event = new Event.@this();  // no upstream, this IS the upstream

// Actor.Context constructor:
this.Event = new Event.@this(upstream: app.Event);
```

The old `Events` property stays in place (renamed already in the runtime2 cleanup era to `events`/`Events`). Don't remove yet — Stage 5 does that.

### What NOT to do in this stage

- Don't modify `event.on` action handler. It still writes to `Actor.Context.Events.Register(...)` until Stage 2.
- Don't change any fire site (`lifecycle.Before.Run(...)` calls stay as-is until Stage 3).
- Don't delete `Events/Lifecycle/Bindings/Binding/` folders. Stage 5.

Stage 1 ships net-new infrastructure that's reachable only from tests until later stages light it up.
