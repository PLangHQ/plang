# Stage 3: `keepalive-collection`

**Read first:**
- `plan/principles.md` — OBP discipline, especially smell #1 (public mutable collection with rules enforced from outside).
- `plan/scope-map.md` — KeepAlive will be App-level (shared per app); single-instance, allocated by App.

**Goal:** Move the `_keepAlive` private list and its `KeepAlive(x)` / `RemoveKeepAlive(x)` methods on App into their own `KeepAlive.@this` collection type. App holds the collection as a property; `app.KeepAlive.Add(x)` / `app.KeepAlive.Remove(x)` becomes the surface; the foreach-and-dispose loop in `App.DisposeAsync` becomes `await KeepAlive.DisposeAsync()`.

**Scope:**
- *Included:* create `App/KeepAlive/this.cs` (new folder + new file); move the list, the Add/Remove logic, and the dispose-each-and-clear pattern into it; expose `app.KeepAlive` on App; replace the foreach in `App.DisposeAsync` with the delegated call; delete `App.KeepAlive(x)` and `App.RemoveKeepAlive(x)` (zero external callers — verified).
- *Excluded:* the other foreach-disposes in `App.DisposeAsync` (`_modules.All`, `Providers.All()`) — those are stage 4's scope. Don't touch them in stage 3.

**Deliverables:**
- `PLang/App/KeepAlive/this.cs` — **new file**. Sketched below. Implements `IAsyncDisposable`. Contains the list (private), `Add(object)`, `Remove(object)`, `DisposeAsync()` that disposes each entry and clears.
- `PLang/App/this.cs`:
  - Delete `private readonly List<object> _keepAlive = new();` (line 24).
  - Delete `public void KeepAlive(object instance) => _keepAlive.Add(instance);` (line 270).
  - Delete `public void RemoveKeepAlive(object instance) { ... }` (lines 273–280, including the inline sync-dispose at line 278).
  - Add `public KeepAlive.@this KeepAlive { get; } = new();` somewhere in the App-property block (near other shared subsystems like `Modules`, `Providers`).
  - In `DisposeAsync` (lines 685–691 today), replace the foreach + Clear with `await KeepAlive.DisposeAsync();`.
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** None. Independent of stages 1 and 2.

## Design

### The smell this closes

**Smell #1** — *public mutable collection with rules enforced from outside*. Today `_keepAlive` is private, but the *behavior* (Add, Remove, dispose-each, clear) is split between `App.this.cs` (the methods) and `App.DisposeAsync` (the dispose loop). Three concerns spread across one file, all about one collection. The collection should own its own discipline; App should hold a reference and delegate.

**Smell #4** — *allocate-here / mutate-there / clean-up-elsewhere*. The list is allocated at field-init (line 24), mutated by Add/Remove (lines 270, 277), iterated for dispose at line 686, cleared at line 691. Four sites for one collection's lifecycle.

### The new shape

**`PLang/App/KeepAlive/this.cs`** (new file):

```csharp
namespace App.KeepAlive;

/// <summary>
/// App-level "keep alive" collection. Disposable objects added here live
/// for the life of the App and get disposed on App.DisposeAsync. One per app.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private readonly List<object> _items = new();
    private bool _disposed;

    /// <summary>Promotes an object to app-level lifetime. Disposed on DisposeAsync.</summary>
    public void Add(object instance) => _items.Add(instance);

    /// <summary>
    /// Removes the object from the collection AND disposes it synchronously.
    /// (Mirrors the prior App.RemoveKeepAlive semantics — sync dispose because the
    /// caller may be reaching from a non-async path.)
    /// </summary>
    public void Remove(object instance)
    {
        if (!_items.Remove(instance)) return;
        if (instance is IAsyncDisposable ad) ad.DisposeAsync().AsTask().GetAwaiter().GetResult();
        else if (instance is IDisposable d) d.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var item in _items)
        {
            if (item is IAsyncDisposable ad) await ad.DisposeAsync();
            else if (item is IDisposable d) d.Dispose();
        }
        _items.Clear();
    }
}
```

This is a faithful translation of today's behavior — same Add semantics, same Remove-with-sync-dispose semantics, same DisposeAsync semantics. No new behavior.

**App.DisposeAsync** changes from:

```csharp
// Today (lines 685–691):
foreach (var d in _keepAlive)
{
    if (d is IAsyncDisposable asyncKeep) await asyncKeep.DisposeAsync();
    else if (d is IDisposable disposableKeep) disposableKeep.Dispose();
}
_keepAlive.Clear();
```

to:

```csharp
// After:
await KeepAlive.DisposeAsync();
```

App's class shrinks by ~20 lines. `_keepAlive` field, two methods, dispose-loop, and clear are gone.

### Files touched + caller propagation

**Files modified (1) + new (1):**
- `PLang/App/this.cs` — field + two methods deleted; one property added; DisposeAsync block shrunk.
- `PLang/App/KeepAlive/this.cs` — new file (~30 lines).

**Caller verification:**
- `app.KeepAlive(x)` — **zero external callers** (verified by `grep -rn "\.KeepAlive\b\|KeepAlive(" PLang/ PLang.Tests/ Tests/` returning only the method definitions themselves). Safe to delete the methods rather than keep them as delegates.
- `app.RemoveKeepAlive(x)` — same; zero external callers.
- `_keepAlive` — referenced only inside App.this.cs at lines 24, 270, 277, 278, 686, 691. All in the touch list.

**Test impact:** none expected — the behavior is unchanged. If a test indirectly depended on App's KeepAlive method existing as a *public method* (rather than the property), build break catches it. None likely; the method had no callers.

### Risk + dependencies

**Risk: low.** Pure extract-class refactor with zero external callers. The new type is a faithful translation of the prior in-place logic.

Possible failure modes:
1. **A grep miss on KeepAlive callers** — unlikely; multiple grep patterns scanned, all returned only the definitions.
2. **Missing the global alias for the new type** — if you find `App/GlobalUsings.cs` or similar adds a global alias for KeepAlive (unlikely; not all subsystems get one), keep parity.
3. **DisposeAsync ordering** — the new call replaces the old foreach in-place; ordering is preserved (KeepAlive disposes after Modules and Providers, same as today).

**Dependencies: none.** Independent of stages 1, 2, and 4.

### Tests

**No new tests required.** Behavior unchanged.

**Existing test coverage to verify:**
- `PLang.Tests/App/Core/EngineTests.cs` — App lifecycle + dispose flow.
- `Tests/` — full PLang suite.

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `grep -n "_keepAlive" PLang/App/` — zero hits.
- `grep -n "public.*KeepAlive(\|RemoveKeepAlive" PLang/App/this.cs` — zero hits (the methods are gone; the new property `public KeepAlive.@this KeepAlive { get; }` is fine).

### Watch for (coder eyes-on)

- **Other private collections on App with rules-enforced-from-outside.** While reading App.this.cs you'll see the same shape may exist for other subsystems (handler lists, channels, etc.). Per stage 4, `_modules.All` and `Providers.All()` are about to get the same realignment — but stage 3 doesn't touch them. Flag if you see a *different* one.
- **The Remove-with-sync-dispose semantics** — preserved here because today's `RemoveKeepAlive` does inline `.GetAwaiter().GetResult()`. If you read the original and conclude the sync-dispose is itself a smell, flag it for a future stage but don't change semantics in stage 3.
- **Boot-order dependency on KeepAlive existence** — App's ctor allocates the collection at field init (`new()`). Verify no boot-time code path adds to KeepAlive *before* App's ctor completes. Today's `_keepAlive = new()` works the same way; the new property `KeepAlive { get; } = new();` should too.

### Stages that follow this one

- **Stage 4** (`dispose-self-owns`) — same Tier 1 batch. Same App.DisposeAsync method gets two more foreach loops replaced (Modules and Providers). Independent of stage 3 mechanically; both can land in either order.

### Out of scope

- Modules / Providers self-disposing — stage 4.
- Renaming `KeepAlive` to anything else — stage 15 (compound-name-rename) territory if it ever applies.
- Dropping `Modules.All` (which becomes unused after stage 4) — stage 4's job to evaluate, not stage 3.

## Commit plan

```
runtime2-cleanup stage 3: KeepAlive becomes its own collection

The _keepAlive list on App had its lifecycle scattered across four
sites: field allocation, KeepAlive(x) method, RemoveKeepAlive(x)
method, dispose-each foreach + Clear in DisposeAsync. Smell #1
(public mutable collection with rules enforced from outside) and
smell #4 (allocate-here / mutate-there / clean-up-elsewhere) on the
same shape.

New: App/KeepAlive/this.cs — a sealed @this class with private list,
Add(object), Remove(object) (with the same sync-dispose semantics
RemoveKeepAlive had), and IAsyncDisposable. One per app, allocated
on App via field-init.

App: _keepAlive field gone; KeepAlive(x) and RemoveKeepAlive(x)
methods gone (zero external callers — verified). App.DisposeAsync's
6-line foreach replaced with `await KeepAlive.DisposeAsync()`.
```
