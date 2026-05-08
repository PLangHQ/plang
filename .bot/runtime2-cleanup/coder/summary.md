# coder — runtime2-cleanup

## Version

v12 — Stage 12 (`build-branch-to-build-this`).
v11 — Stage 11 (`errors-app-backref-drop`).
v10 — Stage 10 (`app-run-redesign`).
v9 — Stage 9 (`catalog-dissolve-to-modules-schema`).
v8 — Stage 8 (`read-file-off-channels`).
v7 — Stage 7 (`callstack-promote-app-property`).
v6 — Stage 6 (`app-data-inheritance-drop`).
v5 — Stage 5 (`getstatic-shim-drop`).
v4 — Stage 4 (`dispose-self-owns`).
v3 — Stage 3 (`keepalive-collection`).
v2 — Stage 2 (`channels-v1-helpers-drop`).
v1 — Stage 1 (`serializers-single-home`).

---

## v12 — Stage 12 (`build-branch-to-build-this`)

Build-mode bootstrap moves out of App.Start. Build.@this gains
`RunAsync()` that owns the app.pr existence check, headless guard,
interactive y/n prompt for new-app creation, channel-wiring guard,
`CurrentActor = User` switch, and Build goal dispatch. All `app.X`
reaches use Build's existing `_app` field.

App.Start's build branch shrinks from 33 lines to:

```csharp
if (Build.IsEnabled) return await Build.RunAsync();
```

C# 2755/2755 pass; PLang 199/199 pass.

## v11 — Stage 11 (`errors-app-backref-drop`)

Eliminate the post-construction injection `Errors.App = this;` at
`App.this.cs:297`. `Errors.@this` takes App via constructor:
`public @this(App.@this app) { _app = app; }`. The `internal App.@this?
App { get; set; }` property is gone; `_app` is `private readonly` and
non-null.

Inside Push, all `App?.CallStack` / `App!.Variables` / `e.App = App`
references collapse to `_app.CallStack` / `_app.Variables` / `e.App = _app`
— the `if (stack != null)` guard goes away with them.

App's ctor allocates `Errors = new global::App.Errors.@this(this);` at
the line where the post-construction setter used to be.

Test sweep: `PLang.Tests/App/Errors/ErrorsScopeTests.cs` had 7 sites
constructing `new Errors.@this()` directly. Each now creates a real
App via `await using var app = new global::App.@this("/test");` and
reads `app.Errors`. Per-test App, auto-disposed.

C# 2755/2755 pass; PLang 199/199 pass.

---

## v10 — Stage 10 (`app-run-redesign`)

### What this is

The headliner. `App.Run` was 85 lines with ~10 foreign mutations on
`context` (Step, Goal, Event, Step.Context) plus an inline try/catch that
stamped errors with `SnapshotParams` and `CallFrames`. Two new
abstractions extract those concerns to their natural owners:

- `Context.AnchorScope(action)` — IDisposable that captures the
  Step/Goal/Event/Step.Context anchors, sets them to the action's, and
  restores on Dispose.
- `Call.ExecuteAsync(handler, context)` — runs the handler, stamps
  errors with `SnapshotParams` + `SnapshotChain()`, adds to `Errors` +
  `_stack.Audit`, and swallows OCE into a `ServiceError` (timeout.after
  contract).

App.Run now reads as: get handler → push call (with overflow catch) →
AnchorScope → `call.ExecuteAsync(handler, context)`.

### What was done

**`PLang/App/Actor/Context/this.cs`** — new `AnchorScope(Action action)`
method + private `AnchorScopeDisposable` struct. The struct captures
prevStep / prevGoal / prevEvent / prevStepContext on construction (the
caller-side AnchorScope sets the new anchors immediately after); Dispose
restores. `action.Step.Context = this` swap (the parallel-Task.WhenAll
guard) lives inside AnchorScope's setter.

**`PLang/App/CallStack/Call/this.cs`** — new `ExecuteAsync(ICodeGenerated
handler, Actor.Context.@this context)` instance method. Reads
`this.Action`, `this.Errors`, `this._stack.Audit`, `this.SnapshotChain()`
— no extra parameters. Holds the handler-execution try/catch plus the
OCE swallow. Same error-stamping order as before (Params then
CallFrames; both gated on "if not already set").

**`PLang/App/this.cs`** — `App.Run` collapses from 85 lines to ~15:

```csharp
public async Task<Data.@this> Run(Action action, Context context, Call? cause = null)
{
    var (handler, error) = Modules.GetCodeGenerated(action);
    if (error != null) return Data.@this.FromError(error);

    Call call;
    try { call = CallStack.Push(action, context.Variables, cause); }
    catch (CallStackOverflowException ex) { return HandleOverflow(ex, action.Step, CallStack); }

    await using var _ = call;
    using var _anchor = context.AnchorScope(action);
    return await call.ExecuteAsync(handler!, context);
}
```

`HandleOverflow` stays in App.Run as a private helper because overflow
trips at Push-time before the Call frame exists — can't fold into
ExecuteAsync.

### Behaviour preserved precisely

- `CallStackOverflowException` catch tight to `CallStack.Push` only.
- OperationCanceledException swallowed inside `Call.ExecuteAsync` only —
  App.Run's outer flow doesn't catch it.
- Error stamping order: Params → CallFrames (matches today).
- Dispose order: `await using var _ = call;` declared *before*
  `using var _anchor = ...;` so reverse-order disposal runs the sync
  anchor restore first, then the async Call dispose. This mirrors the
  old code's `try/finally`-then-`await using` ordering.
- The `action.Step.Context = context` swap (shared-Step parallel-dispatch
  guard) replicated in AnchorScope's setter.

### Verification

- `dotnet build PlangConsole` clean (0 errors).
- C# 2755/2755 pass.
- PLang 199/199 pass.
- App.Run + HandleOverflow combined: ~30 lines including doc-comments
  (vs 85 lines before). Happy path is 6 lines.

### Notes

- No file relocations, no caller sweeps. Both new methods are internal
  to App.Run's flow.
- Step alias (`global using Step = App.Goals.Goal.Steps.Step.@this`)
  already in scope — used in the `HandleOverflow` signature.

---

## v9 — Stage 9 (`catalog-dissolve-to-modules-schema`)

### What this is

`App/Catalog/` dissolves entirely into `App/Modules/Schema/`. The Catalog
concept is "Modules describing itself to the LLM" — Modules has the data,
Schema is now a property of Modules. Rule E refactor: callers stop
decomposing `app.Modules` to pass it as a parameter; `Build()` and
`Render(spec)` are instance methods that navigate `_modules` internally.

### What was done

**Folder relocations** (5 files moved, types renamed):

- `App/Catalog/this.cs` → `App/Modules/Schema/this.cs` (`Schema.@this`)
- `App/Catalog/ActionSpec.cs` → `App/Modules/Schema/Spec/Action.cs` (record renamed `ActionSpec` → `Action`)
- `App/Catalog/ExampleSpec.cs` → `App/Modules/Schema/Spec/Example.cs` (record renamed `ExampleSpec` → `Example`)
- `App/Catalog/TypeEntry.cs` → `App/Modules/Schema/Entry.cs` (`TypeEntry` → `Entry`, `TypeKind` → `EntryKind`, `Field` keeps name)
- `App/Catalog/ExampleRenderer.cs` → `App/Modules/Schema/Render.cs` — folded as a `partial class @this`; all private statics converted to instance methods reading `_modules`

**Deleted**: `App/Catalog/ExampleHelpers.cs` (the static `Example(intent, chain)`
and `Action("module.action", ...)` helpers — callers switch to record positional
ctors `new ExampleSpec(...)` / `new ActionSpec("module", "name", ...)`).

**Modules.@this gains `Schema` property**:

```csharp
public Schema.@this Schema { get; }

public @this()
{
    Schema = new Schema.@this(this);
    Discover(typeof(@this).Assembly, "App.modules");
}
```

The `this` passed to the Schema ctor is the back-reference Schema
navigates for `_modules.GetActionType(...)` lookups.

**Lazy semantics preserved**: `app.Modules.Schema` returns the unbuilt
host instance (empty `PrimitiveNames` / `Types`). `Build()` returns a
fresh Schema with both populated. `Render(spec)` works on the host
without `Build` being called first (Render only needs `_modules`).

### Caller sweeps

**Production (4 files)**:

- `Modules/this.cs:289-294` — `App.Catalog.ExampleSpec[]` →
  `App.Modules.Schema.Spec.Example[]`; the static
  `App.Catalog.ExampleRenderer.Render(s, this)` becomes `Schema.Render(s)`
  (uses Modules' own held Schema).
- `Types/this.cs:372` — type-name rename.
- `Utils/TypeMapping.cs` — 10 sites: `TypeEntry`/`TypeKind`/`Field`
  references renamed.
- `modules/builder/providers/DefaultBuilderProvider.cs:37` —
  `App.Catalog.@this.Build(action.Context.App.Modules)` →
  `action.Context.App.Modules.Schema.Build()`. Rule E migration: caller
  stops passing modules in.

**Action handlers (6 files)** — `using static App.Catalog.ExampleHelpers;`
dropped, `Example(...)` and `Action(...)` helper calls rewritten to
positional ctors. Type aliases used to disambiguate `Action` (record)
from `System.Action` (delegate) and from the `[Action]` attribute:

```csharp
using ExampleSpec = App.Modules.Schema.Spec.Example;
using ActionSpec  = App.Modules.Schema.Spec.Action;

new ExampleSpec("intent", new[]
{
    new ActionSpec("file", "read", new() { ["Path"] = "%path%" },
        Modifiers: new[] { new ActionSpec("error", "handle", ...) }),
});
```

The dot-split (`"file.read"` → `"file", "read"`) is now done by the
caller — the helper that did it is gone.

Files: `error/handle.cs`, `math/{add,subtract,multiply,divide,power}.cs`.

**Tests (4 files)**:

- `PLang.Tests/App/Catalog/CatalogTests.cs` → relocated as
  `PLang.Tests/App/Modules/Schema/SchemaTests.cs`. Body updated to
  `_app.Modules.Schema.Build()`, `EntryKind` references, etc.
- `PLang.Tests/App/Modules/builder/ComplexTypeDiscoveryTests.cs` —
  type-name sweep.
- `PLang.Tests/App/Modules/builder/GetTypeInfoTests.cs` — `App.Catalog.@this`
  → `App.Modules.Schema.@this`.
- `PLang.Tests/App/Modules/math/MathExamplesForLlmTests.cs` — caller
  swept (was missed by the brief's grep — `using App.Catalog;` form).
  `ExampleRenderer.Render(spec, _app.Modules)` → `_app.Modules.Schema.Render(spec)`.

### Verification

- `find PLang/App/Catalog -type f` → empty (folder gone).
- `find PLang.Tests/App/Catalog -type f` → empty (folder gone).
- `grep -rn "App\.Catalog\." PLang/ PLang.Tests/ Tests/ --include='*.cs'` → 0.
- `grep -rn "ExampleHelpers" PLang/ PLang.Tests/ Tests/ --include='*.cs'` → 0.
- C# 2755/2755 pass; PLang 199/199 pass.

### Notes

- Architect anticipated 12 ExampleHelpers-using handlers; actual count
  was 6 (`error/handle.cs` + 5 math files). Brief's grep was conservative.
- One test file (`MathExamplesForLlmTests.cs`) was missed by the brief's
  grep because it used `using App.Catalog;` (namespace import) rather
  than `using static App.Catalog.ExampleHelpers;`. Caught at C# test run.
- The static formatters in `DefaultBuilderProvider` (`FormatValue`,
  `RenderActionFormal`) and `FluidProvider` (`FormatFormalValue`) — flagged
  by the architect as out-of-scope — are still in place. Future stage.

---

## v8 — Stage 8 (`read-file-off-channels`)

Pure dead-code deletion. `Channels.@this.ReadAsync<T>(string filePath, ...)`
read a file from disk and deserialised — never touched a channel — and had
zero callers across PLang/, PLang.Tests/, Tests/. Plan one-liner anticipated
relocating to `app.Serializers` or FileSystem; both findings made relocation
moot (zero callers + `app.Serializers` deleted in stage 1). Just deleted.

C# 2755/2755 pass; PLang 199/199 pass.

## v7 — Stage 7 (`callstack-promote-app-property`)

`app.Debug.CallStack` promoted to `app.CallStack`. Same instance, same scope
(one shared per app) — only the property location moves to align with the
folder/namespace.

Added `public CallStack.@this CallStack { get; } = new();` on App. Removed
the property + ctor allocation from Debug; Debug's one internal use
(`CallStack.Flags = ...` in `Apply`) reaches via its existing `_engine`
field. Context's read-through accessor updated.

Brief listed 7 production caller sites; grep surfaced 2 extras
(`Variables/this.SnapshotAt.cs:19`, `Errors/this.cs:70`). Plus 11 test
files swept (mainly `PLang.Tests/App/CallStackTests/`,
`App/Debug/DebugCallStackParseTests.cs`,
`App/Modules/debug/TagActionTests.cs`). One stale doc-comment in
`App/CallStack/this.cs:7` refreshed.

C# 2755/2755 pass; PLang 199/199 pass.

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
