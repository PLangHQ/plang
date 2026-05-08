# Stage 4: `dispose-self-owns`

**Read first:**
- `plan/principles.md` — OBP discipline, especially smell #1 (collection rules enforced from outside) and smell #4 (allocate-here / mutate-there / clean-up-elsewhere).
- `plan/scope-map.md` — Modules and Providers are App-level (shared per app); they take App.

**Goal:** `Modules.@this` and `Providers.@this` implement `IAsyncDisposable` themselves and own their own dispose iteration. `App.DisposeAsync` stops peeking into `_modules.All` and `Providers.All()` to dispose their contents — instead it calls `await Modules.DisposeAsync()` and `await Providers.DisposeAsync()`.

**Scope:**
- *Included:* implement `IAsyncDisposable` on both `App.Modules.@this` and `App.Providers.@this`; each iterates its own internal collection and disposes each entry (same logic as today, just on the right owner); replace the two foreach blocks in `App.DisposeAsync` with the delegated calls.
- *Excluded:* the `_keepAlive` foreach — that's stage 3's scope. Don't touch it in stage 4.
- *Excluded:* `Modules.All` and `Providers.All()` public surface — these become unused as dispose-iterators after stage 4 lands, but they may have other uses (or simply be dead code to remove later). Stage 4 doesn't decide; it just stops using them for dispose.

**Deliverables:**
- `PLang/App/Modules/this.cs` — class declaration changes from `public sealed class @this` to `public sealed class @this : IAsyncDisposable`. Add `public async ValueTask DisposeAsync()` method that iterates `_modules.Values.SelectMany(a => a.Values).Where(e => e.Instance != null).Select(e => e.Instance!)` (same projection as `All`) and disposes each handler the same way today's `App.DisposeAsync` does (IAsyncDisposable preferred, IDisposable fallback). Add a `_disposed` guard to make repeat-call safe.
- `PLang/App/Providers/this.cs` — same shape. Class becomes `public sealed partial class @this : IAsyncDisposable`. Add `DisposeAsync` iterating `_providers.Values.SelectMany(...)` (same projection as `All()`) and disposing each provider with the same IAsyncDisposable/IDisposable pattern.
- `PLang/App/this.cs` — in `DisposeAsync` (lines 667–683 today), replace the two foreach blocks (`_modules.All` and `Providers.All()`) with `await Modules.DisposeAsync(); await Providers.DisposeAsync();`. Method shrinks by ~16 lines.
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** None. Independent of stages 1, 2, and 3.

## Design

### The smell this closes

**Smell #4** — *allocate-here / mutate-there / clean-up-elsewhere*. Today the iteration-and-dispose lives in `App.DisposeAsync` (lines 668–683) but the data being iterated lives on `Modules.@this` and `Providers.@this`. App reaches across class boundaries to do cleanup work that belongs to those types. Each subsystem should own its own discipline — including teardown.

The pattern reads "App is the cleanup boss for things it doesn't own." That's the smell. After stage 4: each subsystem is responsible for itself; App calls `await X.DisposeAsync()` for each subsystem that owns disposable contents.

### The new shape

**`Modules.@this`:**

```csharp
// Today (line 14):
public sealed class @this

// After:
public sealed class @this : IAsyncDisposable

// New method, anywhere appropriate in the file:
public async ValueTask DisposeAsync()
{
    if (_disposed) return;
    _disposed = true;
    foreach (var entry in _modules.Values
                                  .SelectMany(a => a.Values)
                                  .Where(e => e.Instance != null))
    {
        var handler = entry.Instance!;
        if (handler is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (handler is IDisposable disposable)
            disposable.Dispose();
    }
}

// And a private bool _disposed; field.
```

The iteration mirrors `Modules.All`'s projection (line 153–156). Pre-existing comment on `All` says "All registered instances (for disposal on app shutdown)" — that purpose is now met inside the class.

**`Providers.@this`:**

```csharp
// Today (line 16, partial class declaration):
public sealed partial class @this

// After:
public sealed partial class @this : IAsyncDisposable

// New method:
public async ValueTask DisposeAsync()
{
    if (_disposed) return;
    _disposed = true;
    foreach (var provider in _providers.Values.SelectMany(p => p.Values))
    {
        if (provider is IAsyncDisposable asyncProv)
            await asyncProv.DisposeAsync();
        else if (provider is IDisposable disposableProv)
            disposableProv.Dispose();
    }
}

// And a private bool _disposed; field.
```

Mirrors `Providers.All()` (line 109)'s projection.

**App.DisposeAsync** changes from:

```csharp
// Today (lines 667–683):
// Dispose any disposable handlers
foreach (var handler in _modules.All)
{
    if (handler is IAsyncDisposable asyncDisposable)
        await asyncDisposable.DisposeAsync();
    else if (handler is IDisposable disposable)
        disposable.Dispose();
}

// Dispose providers (HttpClient, etc.)
foreach (var provider in Providers.All())
{
    if (provider is IAsyncDisposable asyncProv)
        await asyncProv.DisposeAsync();
    else if (provider is IDisposable disposableProv)
        disposableProv.Dispose();
}
```

to:

```csharp
// After:
await Modules.DisposeAsync();
await Providers.DisposeAsync();
```

### Files touched + caller propagation

**Files modified (3):**
- `PLang/App/Modules/this.cs` — interface added; method added; field added.
- `PLang/App/Providers/this.cs` — interface added; method added; field added.
- `PLang/App/this.cs` — two foreach blocks (~16 lines) replaced with two delegated calls.

**Caller verification:**
- `_modules.All` (the foreach iterator) — only consumer is `App.this.cs:668` (verified). After stage 4, `Modules.All` has zero callers. **This is fine for stage 4** — `Modules.All` stays as public surface. A future stage may evaluate whether to remove it. Don't preempt.
- `Providers.All()` — same shape. Only consumer is `App.this.cs:677`. After stage 4, no callers; surface stays.

**Test impact:** none expected — observable behavior is identical. App's DisposeAsync runs the same disposal sequence; just delegates.

### Risk + dependencies

**Risk: low.** The disposal logic is a faithful translation of the existing foreach blocks. The only new mechanism is the `_disposed` guard on each subsystem (mirroring the pattern App already uses at line 652–655).

Possible failure modes:
1. **Disposal order matters between Modules and Providers.** Today the order is _modules first, then Providers. The brief preserves that order. If a provider depends on a module being alive at dispose time (unlikely; usually the other way), this matters — but the order is preserved, so no change.
2. **Re-dispose safety.** Today's App.DisposeAsync has `if (_disposed) return;` at line 652. The subsystem DisposeAsync methods need their own guards (`_disposed` field) so a future caller could call them independently without crashing.
3. **Modules.All projection drift.** The new `Modules.DisposeAsync` should iterate the same projection that `All` uses today. Don't accidentally include type-registered (per-call) actions that today's `All` filters out via `.Where(e => e.Instance != null)`.

**Dependencies: none.** Independent of stages 1, 2, 3.

### Tests

**No new tests required.** Behavior unchanged.

**Existing test coverage to verify:**
- `PLang.Tests/App/Core/EngineTests.cs` — App lifecycle + dispose flow.
- Anything that tests provider or module disposal — verify the new dispose path works the same.
- `Tests/` — full PLang suite.

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `grep -n "_modules\.All\|Providers\.All()" PLang/App/this.cs` — zero hits (those iterators are not used in DisposeAsync anymore).
- The two new `DisposeAsync` methods exist on Modules.@this and Providers.@this.

### Watch for (coder eyes-on)

- **`Modules.All` / `Providers.All()` becoming dead public surface.** After stage 4, neither has any caller. Don't remove in stage 4 — flag for a future "cleanup unused public surface" pass if you have an opinion.
- **Other subsystems with the same shape.** While reading `App.DisposeAsync`, you may see sibling foreach-and-dispose loops that aren't in stage 3 or 4's scope. Today's only siblings are `_keepAlive` (stage 3) and the actor disposal at lines 662–665 (already self-disposing — `_system.DisposeAsync()` is a delegated call). If you find another, flag it.
- **Disposal ordering in App.DisposeAsync after both stages land.** Final order should be:
  1. `_shutdownCts.Cancel(); _shutdownCts.Dispose();`
  2. `await _system.DisposeAsync();`
  3. `await _user.DisposeAsync();`
  4. `await Modules.DisposeAsync();`
  5. `await Providers.DisposeAsync();`
  6. `await KeepAlive.DisposeAsync();`
  
  If you're landing both stages in the same session, verify this order matches today's logical sequence (actors first → handlers → providers → keep-alive). It does.

### Stages that follow this one

- **Stage 3** (`keepalive-collection`) — same Tier 1 batch. Either order works; both touch `App.DisposeAsync` in different sections.
- No other stage depends on stage 4.

### Out of scope

- Removing `Modules.All` / `Providers.All()` public surface (now dead) — separate cleanup decision.
- Reorganizing or renaming `Modules` or `Providers` — stage 19 (Provider→Code rename) handles Providers; Modules stays as-is per the plan.
- The `_keepAlive` dispose loop — stage 3.

## Commit plan

```
runtime2-cleanup stage 4: Modules and Providers self-dispose

App.DisposeAsync today reaches across class boundaries to do cleanup
work that belongs elsewhere — foreach loops over _modules.All and
Providers.All() to dispose registered handlers and providers. Smell #4
(allocate-here / mutate-there / clean-up-elsewhere) at the lifecycle
layer.

Modules.@this and Providers.@this now implement IAsyncDisposable.
Each owns its own dispose iteration: Modules iterates _modules.Values
and disposes registered instances; Providers iterates _providers.Values
and disposes each provider. Both have a _disposed guard so re-dispose
is safe.

App.DisposeAsync replaces the two ~8-line foreach blocks with:
  await Modules.DisposeAsync();
  await Providers.DisposeAsync();

Same dispose order, same dispose pattern (IAsyncDisposable preferred,
IDisposable fallback), same handler filter. Modules.All and
Providers.All() lose their only callers but stay as public surface
for now — separate decision whether to remove later.
```
