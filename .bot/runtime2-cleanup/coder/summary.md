# coder — runtime2-cleanup

## Version

v21 — Stage 21 (`navigators-to-variables`).
v17 — Stage 17 (`builder-tester-rename`).
v20 — Stage 20 (`channel-app-backref-drop`).
v14 — Stage 14 (`timespan-iso-8601-sweep`).
v13 — Stage 13 (`settings-collection-rework`).
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

## v20 — Stage 20 (`channel-app-backref-drop`)

After stage 1 added `Channel.@this.Channels` (parent collection back-ref),
the existing `Channel.@this.App` was redundant — App reachable through
the parent chain. Stage 20 drops the redundant ref.

- Deleted `public global::App.@this? App { get; internal set; }` and its setter line in `Channels.Register`.
- Updated **two** readers (the brief listed one — found a second at the diagnostic Write line 249): `App?.X` → `Channels?.App?.X`.

The brief proposed `Channels.Actor.App` chain; running the cross-actor
binding test surfaced that **Service-owned Channels have no Actor**
(Service holds Channels but isn't an Actor). Exposed `Channels.@this.App`
as a public property instead — always non-null, covers both
actor-owned and service-owned cases. Internal `App.Data.*` /
`App.Errors.*` references inside `Channels.this.cs` were qualified with
`global::` because the new `App` property shadows the namespace within
the class.

C# 2752/2752 pass; PLang 199/199 pass.

## v14 — Stage 14 (`timespan-iso-8601-sweep`)

`int? ExpiresInMs` → `TimeSpan? Expires` on Callback.Signature and the
`signing.sign` action. JSON wire form becomes ISO 8601 (`"PT5M"`) via
the existing globally-wired `TimeSpanIso8601Converter`.

### Changes

- `Callback/Signature/this.cs` — `int? ExpiresInMs` → `TimeSpan? Expires`; doc refresh.
- `modules/signing/sign.cs` — `Data.@this<int>? ExpiresInMs` → `Data.@this<TimeSpan>? Expires`.
- `Data/this.Envelope.cs` — local typed `TimeSpan?`; envelope assignment uses `new @this<TimeSpan>("", expires.Value)`.
- `Ed25519Provider.cs:47` — `now.AddMilliseconds(expiryMs)` → `now.Add(expiry)` (TimeSpan-typed).
- `DefaultHttpProvider.cs:389` — `signOptions.ExpiresInMs` → `signOptions.Expires`.
- Doc-comment refresh in `App/this.cs` and `Callback/this.cs`.

### Test sweep (5 files)

- `AppCallbackConfigTests` — values switch to `TimeSpan.FromMinutes(5)`; test renamed.
- `DataLazySignatureTests`, `DataContextWiringTests` — values to `TimeSpan.FromSeconds(30)` / `TimeSpan.FromMinutes(1)`.
- `SignActionTests`, `VerifyActionTests` — helper signatures `int? expiresInMs` → `TimeSpan? expires`; call sites use `TimeSpan.FromMilliseconds(50)` / `TimeSpan.FromSeconds(5)`.

### Closes 2026-05-06 todos.md entry

`Documentation/Runtime2/todos.md` marked RESOLVED with note about other
`*Ms` properties flagged for future.

C# 2752/2752 pass; PLang 199/199 pass.

---

## v13 — Stage 13 (`settings-collection-rework`)

### What this is

Three tightly-coupled changes that close the SettingsVariable smell and
move SettingsStore to its right scope:

1. `Settings.@this` becomes a collection-over-Data, not a Data subclass.
2. `SettingsStore` moves from per-actor to app-level.
3. `Variables.RegisterNavigable(name, resolver)` — generalisable
   non-Data navigable mount mechanism.

Plus: `ISettingsStore` → `IStore`, `SqliteSettingsStore` → `Sqlite`,
`SettingsVariable` deleted (absorbed into `Settings/this.cs`).

### What was done

**`PLang/App/Settings/`**:

- `IStore.cs` (renamed from `ISettingsStore.cs`) — same contract,
  shorter name; `IStore.ResolveTableName(System.Type)` static helper.
- `Sqlite.cs` (renamed from `SqliteSettingsStore.cs`) — class becomes
  `Sqlite : IStore`; `Sqlite.InMemory(name)` factory unchanged.
- `this.cs` — NEW `Settings.@this` (sealed). Two-method surface:
  `Get(path, context)` loads the first segment from the store and
  navigates remainder via `Data.GetChild`; `Set(key, data)` writes
  through. No inheritance.
- `SettingsVariable.cs` — DELETED (its dual-mode `Data.@this` subclass
  with overridden `GetChild` was the inheritance smell).

**`PLang/App/this.cs`**:

- `internal SettingsVariable SettingsVariable { get; }` deleted.
- New `public IStore SettingsStore => _settingsStore.Value;` (lazy
  `Lazy<IStore>` — preserves "create on first access" so tests with
  bogus paths don't pay for SQLite-file creation at boot, and the
  PLang test suite's path-rooted FS doesn't blow up during App ctor).
- New `public Settings.@this Settings { get; }` allocated eagerly.
- New `private IStore CreateSettingsStore()` — Testing → in-memory
  scoped by App.Id; otherwise file-backed at `.db/system.sqlite`
  (logic moved verbatim from `Actor.CreateSettingsStore`, hardcoded
  "system" name per architect's settled decision).
- `DisposeAsync` disposes the lazy if it was materialised.

**`PLang/App/Actor/this.cs`**:

- `_dataSource`, `SettingsStore` property, `CreateSettingsStore` method
  all deleted.
- `Context.Variables.Set(app.SettingsVariable.Name, app.SettingsVariable);`
  → `Context.Variables.RegisterNavigable("Settings", path => app.Settings.Get(path, Context));`.
  The lambda captures *this* actor's Context so per-actor ctx propagates
  into Settings.Get when the resolver fires.
- `_dataSource.Value.Dispose()` removed from DisposeAsync (App owns it now).

**`PLang/App/Variables/this.cs`**:

- New `_navigables` dictionary + `public void RegisterNavigable(string name, Func<string, Data.@this> resolver)`.
- `Get` — after `_variables.TryGetValue` fails, checks `_navigables` before returning NotFound.
- `Clone` — shares `_navigables` by reference (resolvers are stateless closures; cloning meaningless). Without this, cloned Variables would NotFound on `%Settings.X%`.
- `Snapshot()` and `this.Snapshot.cs.Capture` — drop the dead `is App.Settings.SettingsVariable` checks (Settings is no longer in `_variables`).

**Production caller sweep (10 sites)**:

- `Goals/Setup/this.cs:108, 143` — `app.System.SettingsStore` → `app.SettingsStore`.
- `modules/identity/providers/DefaultIdentityProvider.cs` (4 sites).
- `modules/llm/providers/OpenAiProvider.cs:58, 837` — and `ISettingsStore` → `IStore`.
- `modules/settings/{get,set,remove}.cs` — store path; `get.cs` uses non-generic `store.Get`; `set.cs` uses `new Data.@this(...)` instead of `new SettingsVariable(...)`.
- `Data/this.Navigation.cs:252` — comment refresh.

**Test sweep (6 files)**:

- `App/Goals/Setup/SetupTests.cs` — 3 sites swept.
- `App/Modules/datasource/DataSourceTests.cs` — `SqliteSettingsStore` → `Sqlite`; `ISettingsStore.ResolveTableName` → `IStore.ResolveTableName`. Two tests on per-actor in-memory premise rewritten as app-level (`Actor_UsesInMemory_WhenBuildingEnabled` deleted — Build mode no longer differentiates).
- `App/Modules/identity/IdentityErrorPathTests.cs` — `SwapDataSource` retargeted from Actor's `_dataSource` to App's `_settingsStore` Lazy field; 11 call sites changed to pass `_app` instead of `_app.System`; `ISettingsStore` → `IStore`.
- `App/Modules/settings/SettingsDataTests.cs` — `new SettingsVariable(...)` → `new Data.@this(...)`; `Variables.Get("Settings").GetChild("X")` patterns rewritten as direct `Variables.Get("Settings.X")` (the navigable resolver dispatches through dot-notation only); `SameObjectAcrossAllActors` test re-purposed to assert app-level `app.Settings` identity + cross-actor read.
- `App/VariablesTests/VariablesSnapshotTests.cs` — drop the dead `is SettingsVariable` assertion.
- `App/Context/ActorSettingsStoreTests.cs` — substantially rewritten. The "User has its own store" premise is gone; tests narrowed to "app.SettingsStore is in-memory under Testing / file-backed by default". Two User-specific tests deleted.

### Behaviour preserved

- `%Settings.X%` resolves identically: load-from-store on first segment,
  navigate via `Data.GetChild` on remainder, return `AskError` on unset.
- Per-actor Context propagates through the resolver lambda capture.
- Testing.IsEnabled → in-memory scoped by App.Id.
- Default → file-backed at `.db/system.sqlite`.

### Verification

- `find PLang/App/Settings -name 'SettingsVariable.cs'` → empty.
- `grep -rn "SettingsVariable\b" PLang/ PLang.Tests/ Tests/ --include='*.cs'` → 0.
- `grep -rn "\.System\.SettingsStore" PLang/ PLang.Tests/ --include='*.cs'` → 0.
- `grep -rn "_dataSource" PLang/App/Actor/this.cs` → 0.
- `grep -rn "ISettingsStore\b" PLang/ PLang.Tests/ --include='*.cs'` → 0.
- `grep -rn "SqliteSettingsStore\b" PLang/ PLang.Tests/ --include='*.cs'` → 0.
- `dotnet build PlangConsole` clean.
- C# **2752/2752** pass (3 fewer tests than baseline 2755 — User-specific tests deleted).
- PLang **199/199** pass.

### Notes for next stages

- One subtle: App's SettingsStore had to be **lazy** (not eager) — eager
  allocation in App's ctor breaks tests that pass fictional paths
  (`/app`, `/test`) and never touch settings. The old per-actor `Lazy<>`
  was load-bearing for that exact reason; keeping it on App preserves it.
- The InMemory connection-sharing mechanism in `Sqlite.InMemory(name)`
  is unchanged — sentinel connection still works at app level.
- A clean rebuild was needed during this session — the PlangConsole
  binary was stale after the in-place edits and reported a misleading
  NRE inside `App..ctor` line 300; `rm -rf PlangConsole/bin obj && dotnet build`
  fixed it. The CLAUDE.md "Stale-binary trap" note applies.

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
