# coder — runtime2-cleanup

## Version

v6 — Stage 6 (`app-data-inheritance-drop`).
v5 — Stage 5 (`getstatic-shim-drop`).
v4 — Stage 4 (`dispose-self-owns`).
v3 — Stage 3 (`keepalive-collection`).
v2 — Stage 2 (`channels-v1-helpers-drop`).
v1 — Stage 1 (`serializers-single-home`).

---

## v6 — Stage 6 (`app-data-inheritance-drop`)

App stops inheriting from `Data.@this<@this>`. The base list on
`PLang/App/this.cs:19` becomes `: IAsyncDisposable`. The `public new string
Path => "/"` shadow at line 63 is deleted (zero readers — the `new`
keyword was only there because Data had a `Path` property to shadow).
The primary ctor's `: base("!app")` initialiser is dropped (it was
forwarding to Data's ctor).

`this.Snapshot.cs` is the only secondary partial — it never repeated the
base list, so no change there.

Side effect: build warnings drop from 449 → 68 because the inherited-Data
surface generated a flock of nullability warnings that are now gone.

C# 2755/2755 pass; PLang 199/199 pass.

## v5 — Stage 5 (`getstatic-shim-drop`)

`App.GetStatic(string)` was a one-line internal shim delegating to
`Statics.GetBag(key)`. Single caller in `Actor/Context/this.cs:248`
migrated to `App.Statics.GetBag(key)`; shim deleted.

C# 2755/2755 pass; PLang 199/199 pass.

---

## v4 — Stage 4 (`dispose-self-owns`)

### What this is

App.DisposeAsync stops reaching across class boundaries to dispose
contents of `Modules.@this` and `Providers.@this`. Each subsystem owns
its own teardown.

### What was done

- `PLang/App/Modules/this.cs` — `@this` now implements `IAsyncDisposable`.
  Added `_disposed` guard and `DisposeAsync()` that iterates the same
  projection `All` exposes (`_modules.Values.SelectMany(a => a.Values).Where(e => e.Instance != null)`).
- `PLang/App/Providers/this.cs` — `partial @this` now implements `IAsyncDisposable`.
  Same shape: `_disposed` guard + `DisposeAsync()` over `_providers.Values.SelectMany(p => p.Values)`.
- `PLang/App/this.cs` — DisposeAsync's two ~8-line foreach blocks
  collapse to:
  ```csharp
  await _modules.DisposeAsync();
  await Providers.DisposeAsync();
  await KeepAlive.DisposeAsync();
  ```
  Same dispose order, same fallback chain (IAsyncDisposable → IDisposable),
  same handler filter as the prior in-place loops.

### Verification

- `grep -n "_modules\.All\|Providers\.All()" PLang/App/this.cs` → 0.
- `dotnet run --project PLang.Tests` → **2755/2755 pass**.
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` → **199/199 pass**.
- Build clean (0 errors).

### Notes for next stages

`Modules.All` / `Providers.All()` are now dead public readers (only the
DisposeAsync iterations called them, and those now live on the owners).
Architect explicitly left them as public surface — flagged for a future
cleanup pass.

---

## v3 — Stage 3 (`keepalive-collection`)

### What this is

`_keepAlive` private list + `KeepAlive(x)` + `RemoveKeepAlive(x)` + dispose
loop merge into a single `App.KeepAlive.@this` collection that owns Add /
Remove (with sync-dispose semantics) / DisposeAsync. App holds it as a
property.

### What was done

- `PLang/App/KeepAlive/this.cs` — **new file**. `sealed class @this :
  IAsyncDisposable` with private list, `Add(object)`, `Remove(object)` (sync
  dispose preserved), `DisposeAsync()` + `_disposed` guard.
- `PLang/App/this.cs`:
  - Removed `private readonly List<object> _keepAlive = new();`.
  - Removed `public void KeepAlive(object instance)` and
    `public void RemoveKeepAlive(object instance)`.
  - Added `public KeepAlive.@this KeepAlive { get; } = new();`.
  - DisposeAsync's 7-line foreach + Clear → `await KeepAlive.DisposeAsync();`.

### Verification

- `grep -n "_keepAlive" PLang/App/` → 0.
- `grep -n "RemoveKeepAlive" PLang/App/` → 0.
- C# 2755/2755 pass; PLang 199/199 pass; build clean.

### Caller-sweep note

Verified zero external callers of `app.KeepAlive(x)` and
`app.RemoveKeepAlive(x)` across PLang/, PLang.Tests/, Tests/. Methods
deleted outright (no deprecation needed).

---

## v2 — Stage 2 (`channels-v1-helpers-drop`)

Dead-code deletion: removed the two-string `WriteAsync(actorName,
channelName, ...)` overload (zero callers) and the contentType-override
branch + parameter from the single-string `WriteAsync` (zero callers ever
passed contentType). Surviving body shrunk to ~5 lines.

C# 2755/2755 pass; PLang 199/199 pass.

Three remaining `is Channel.Stream.@this sc` casts in `WriteTextAsync`,
`ReadChannelAsync`, `ReadTextAsync` left in place — flagged for future.

---

## v1 — Stage 1 (`serializers-single-home`)

Per-actor `Channels.@this.Serializers` established as the single home;
`App.@this.Serializers` deleted; `Channel.Stream.@this._serializers` field
deleted; new `Channels` back-ref on `Channel.@this` set in
`Channels.Register(channel)` so Stream's `WriteCore` reaches its parent
Channels' Serializers.

5 production callers + 6 test files swept; 7 unit tests updated for the
new boot-ordering (construct-then-write → register-then-write).

C# 2755/2755 pass; PLang 199/199 pass.
