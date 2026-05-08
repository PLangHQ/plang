# Stage 13: `settings-collection-rework`

**Read first:**
- `plan/principles.md` — OBP discipline. Composition over inheritance is foundational; this stage closes the last `Data.@this` subclass that uses inheritance to reach into variable resolution.
- `plan/scope-map.md` — Settings.@this is **shared (one per app)**; SettingsStore is also app-level (relocated from per-actor dead drift). The mixed-cases section settled both 2026-05-08.

**Goal:** Three tightly-coupled changes that together close the SettingsVariable smell and move SettingsStore to its right scope:

1. **Settings.@this becomes a collection over Data**, not a Data subclass. Replaces the `SettingsVariable` class (which today is a Data subclass with overridden `GetChild` to intercept `%Settings.X%` and lazy-load from the store). The new shape is a plain @this that exposes `Get(path, context)` and `Set(key, data)`.

2. **SettingsStore moves from per-actor to App level.** Today `Actor.@this` allocates a `Lazy<ISettingsStore>` per actor (line 15); zero consumers use User's store; every real consumer goes through `app.System.SettingsStore` (10 sites). Per-actor allocation is dead drift. After: one shared `app.SettingsStore` backed by `system.sqlite`.

3. **`Variables.@this.RegisterNavigable(name, resolver)` mechanism** — new method on Variables that wires non-Data navigable mounts. Settings registers itself per-actor: the `%Settings.X%` resolution path goes through this hook instead of a Data-subclass `GetChild` override. Generalizable to any future navigable mount that isn't a Data wrapper.

Plus the renames: `ISettingsStore.cs` → `IStore.cs`, `SqliteSettingsStore.cs` → `Sqlite.cs`. `SettingsVariable.cs` deletes (absorbed into `Settings/this.cs`).

**Scope:**
- *Included:* all three changes above + renames + caller sweeps (10 sites for SettingsStore, 1 site for the SettingsVariable registration).
- *Excluded:* anything else. Don't touch other Settings-adjacent code (Identity, etc.) beyond what the sweep requires.

**Deliverables:**

### Folder + file changes

```
App/Settings/
├── IStore.cs                ← ISettingsStore.cs (rename, drop "Settings" prefix; namespace says it)
├── Sqlite.cs                ← SqliteSettingsStore.cs (rename, drop both "Settings" and "Store" decoration)
└── this.cs                  ← NEW. Absorbs SettingsVariable as a non-inheritance collection.

(SettingsVariable.cs DELETED.)
```

### `Settings/this.cs` (new — replaces SettingsVariable)

```csharp
namespace App.Settings;

/// <summary>
/// Shared (one per app) settings collection. Holds Data values keyed by name.
/// Backed by app.SettingsStore for persistence. Registered on every actor's
/// Variables via Variables.RegisterNavigable so %Settings.X% resolution
/// dispatches into Get(path).
/// </summary>
public sealed class @this
{
    private const string SettingsTable = "settings";
    private readonly App.@this _app;

    public @this(App.@this app) { _app = app; }

    /// <summary>
    /// Loads a setting by path. Path may be a single key ("ApiKey") or
    /// dot-compound ("ApiKey.SubProp"). Compound paths load the first
    /// segment from the store and navigate the result via Data.GetChild.
    /// Returns AskError when the value is unset (matches today's
    /// SettingsVariable.GetChild semantics).
    /// </summary>
    public Data.@this Get(string path, Actor.Context.@this context)
    {
        if (string.IsNullOrEmpty(path)) return Data.@this.NotFound("Settings");

        var dotIndex = path.IndexOf('.');
        var key = dotIndex >= 0 ? path[..dotIndex] : path;
        var remaining = dotIndex >= 0 ? path[(dotIndex + 1)..] : null;

        var result = _app.SettingsStore.Get(SettingsTable, key).GetAwaiter().GetResult();
        if (!result.Success) return result;

        if (result.Value == null)
            return Data.@this.FromError(new App.Errors.AskError(
                $"Settings value '{key}' is not set. Please provide a value.",
                SettingsTable, key));

        result.Context = context;

        return string.IsNullOrEmpty(remaining)
            ? result
            : result.GetChild(remaining);
    }

    /// <summary>Stores a Data value under the given key.</summary>
    public Task<Data.@this> Set(string key, Data.@this value)
        => _app.SettingsStore.Set(SettingsTable, key, value);
}
```

Two-method surface (`Get`, `Set`). No inheritance. No `JsonConstructor` mode (the storage round-trip — today's `JsonConstructor` ctor — moves into the Sqlite implementation; Settings.@this never serializes itself).

### `Settings/IStore.cs` (renamed from ISettingsStore.cs)

Same contract; just the rename. `namespace App.Settings;` and `public interface IStore : IDisposable`.

### `Settings/Sqlite.cs` (renamed from SqliteSettingsStore.cs)

Same impl; class becomes `public sealed class Sqlite : IStore`. The `JsonConstructor` round-trip storage logic stays here (it's about *how* to persist, which is the impl's concern). Today's `SqliteSettingsStore.InMemory(name)` factory keeps the same shape, just `Sqlite.InMemory(name)`.

### `Variables/this.cs` — new RegisterNavigable

Add to `Variables.@this`:

```csharp
private readonly Dictionary<string, Func<string, Data.@this>> _navigables
    = new(StringComparer.OrdinalIgnoreCase);

/// <summary>
/// Register a navigable mount: when Variables.Get encounters `name.X` and
/// `name` isn't found in the regular variable scope, the resolver is called
/// with the path remainder ("X" or "X.Y" etc.) and its result is returned.
///
/// Used by Settings: each actor's Variables registers "Settings" with a
/// resolver that delegates to app.Settings.Get(path, this.Context).
/// Generalizes to any future non-Data navigable mount.
/// </summary>
public void RegisterNavigable(string name, Func<string, Data.@this> resolver)
    => _navigables[name] = resolver;
```

Update `Variables.Get(string name)` (around line 457): after the Calls/`_variables` lookup fails, check `_navigables` before returning NotFound:

```csharp
// Today (lines 491-495):
else if (!_variables.TryGetValue(rootName, out root))
{
    return Data.@this.NotFound(name);
}

// After:
else if (!_variables.TryGetValue(rootName, out root))
{
    if (_navigables.TryGetValue(rootName, out var resolver))
        return resolver(remaining ?? "");
    return Data.@this.NotFound(name);
}
```

### `App.this.cs` — gain SettingsStore + Settings; drop SettingsVariable

```csharp
// Today (line 154):
internal SettingsVariable SettingsVariable { get; }

// After: deleted. Replaced by:
public IStore SettingsStore { get; }
public Settings.@this Settings { get; }

// Today (line 292 in App's ctor):
SettingsVariable = new SettingsVariable(this);

// After (in App's ctor):
SettingsStore = CreateSettingsStore();
Settings = new Settings.@this(this);

// New private method on App (moved from Actor.CreateSettingsStore):
private IStore CreateSettingsStore()
{
    if (Testing.IsEnabled)
        return Sqlite.InMemory($"system-{Id}");

    var dbDir = FileSystem.Path.Combine(/* whatever the dir resolves to */);
    var dbPath = FileSystem.Path.Combine(dbDir, "system.sqlite");
    return new Sqlite(dbPath, FileSystem);
}
```

(Read Actor.CreateSettingsStore at line 149-164 for the actual db-dir resolution; copy verbatim with the actor-name → "system" change. The `dbPath` becomes `system.sqlite` per Ingi's settled call.)

### `Actor.this.cs` — drop SettingsStore; register Settings as navigable

```csharp
// Today (line 15):
private readonly Lazy<ISettingsStore> _dataSource;

// After: deleted.

// Today (line 89):
public ISettingsStore SettingsStore => _dataSource.Value;

// After: deleted.

// Today (line 126 in Actor's ctor):
_dataSource = new Lazy<ISettingsStore>(CreateSettingsStore);

// After: deleted.

// Today (lines 149-164):
private ISettingsStore CreateSettingsStore() { ... }

// After: deleted. (Logic moved to App.CreateSettingsStore.)

// Today (line 133):
Context.Variables.Set(app.SettingsVariable.Name, app.SettingsVariable);

// After:
Context.Variables.RegisterNavigable("Settings", path => app.Settings.Get(path, Context));
```

The `Context` capture in the lambda: each actor's lambda captures *its own* Context. When Variables.Get fires the resolver, the Settings.Get receives the right per-actor Context.

### Caller sweep — `app.System.SettingsStore` → `app.SettingsStore`

10 sites identified:

- `PLang/App/Goals/Setup/this.cs:108` — `app.System.SettingsStore.Exists(...)` → `app.SettingsStore.Exists(...)`.
- `PLang/App/Goals/Setup/this.cs:143` — same.
- `PLang/App/modules/identity/providers/DefaultIdentityProvider.cs:207` — `action.Context.App.System.SettingsStore` → `action.Context.App.SettingsStore`.
- `PLang/App/modules/identity/providers/DefaultIdentityProvider.cs:226` — same.
- `PLang/App/modules/identity/providers/DefaultIdentityProvider.cs:277` — same.
- `PLang/App/modules/identity/providers/DefaultIdentityProvider.cs:285` — same.
- `PLang/App/modules/llm/providers/OpenAiProvider.cs:58` — `app.System.SettingsStore` → `app.SettingsStore`.
- `PLang/App/modules/settings/get.cs:20` — `Context.App.System.SettingsStore` → `Context.App.SettingsStore`.
- `PLang/App/modules/settings/remove.cs:17` — same.
- `PLang/App/modules/settings/set.cs:19` — same.

Plus tests — sweep `PLang.Tests/` for `SettingsStore`.

Plus the `SettingsVariable` reference at App.this.cs:150 (doc comment) and any other references — grep `SettingsVariable\b` after the sweep; should return zero hits.

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `find PLang/App/Settings -type f -name 'SettingsVariable.cs'` — empty.
- `grep -rn "SettingsVariable\b" PLang/ PLang.Tests/ Tests/ --include='*.cs'` — zero hits.
- `grep -rn "app\.System\.SettingsStore\|\.System\.SettingsStore" PLang/ PLang.Tests/ --include='*.cs'` — zero hits.
- `grep -rn "actor\.SettingsStore\|_dataSource" PLang/App/Actor/this.cs` — zero hits.
- `app.SettingsStore` and `app.Settings` exist and reach via `Context.App.SettingsStore` / `Context.App.Settings`.
- `Variables.@this.RegisterNavigable` exists and is reachable.

**Dependencies:** None on stages 11/12. Builds on the trunk; touches files those stages didn't.

## Design

### The smells this closes

**Smell #4 (allocate-here / mutate-there / clean-up-elsewhere)** in two layers:

1. **SettingsStore lifecycle.** Allocated per-actor (`Lazy<ISettingsStore>` on Actor); used app-wide via `app.System.SettingsStore`; user.sqlite gets created but never read. The "where is this allocated" and "where is it used" sit on different types — User's store has zero consumers; the per-actor allocation is dead drift.

2. **SettingsVariable's two construction modes.** Today's `SettingsVariable` is one class with two responsibilities:
   - Runtime mode (with `_app`): mounted on Variables, intercepts `%Settings.X%` via `GetChild` override.
   - Storage mode (`JsonConstructor`): represents a single loaded value, no navigation.
   
   That dual mode is the inheritance smell — `: Data.@this` exists *only* to fit through the GetChild navigation interface. Mechanism leaking into shape.

**The new shape:** Settings owns per-app data only. Storage round-trip lives in Sqlite (an impl of IStore). Variables.@this gets a generalizable `RegisterNavigable` mechanism that any future non-Data mount can use.

### Why three changes are tightly coupled

Each piece independently makes sense, but doing them sequentially would be weird:
- **Settings.@this without RegisterNavigable** would have nowhere to plug into Variables.Get — `%Settings.X%` would just NotFound.
- **RegisterNavigable without Settings using it** is dead infrastructure.
- **App-level SettingsStore without Settings.@this rework** would either need to keep SettingsVariable (still inheriting Data) or have a half-migrated state.

Single stage. Coder lands the three changes together.

### Risk + dependencies

**Risk: medium-high.** Largest stage in the cleanup so far in cross-file design impact. Multiple subtle integration points:

1. **The `result.Context = context` line in Settings.Get** — preserves today's behavior where SettingsVariable's GetChild returns a Data with Context set so further navigation works. Don't drop it.
2. **The lambda closure on `Context`** in Actor's RegisterNavigable call — each actor captures *its own* Context. If you accidentally capture `app.System.Context` or similar, all actors see System's context.
3. **The `app.SettingsStore` allocation timing** — must complete before any code reaches `app.Settings.Get`. Field-init or early ctor placement in App.@this.
4. **The InMemory testing branch** — today's `Actor.CreateSettingsStore` checks `App.Testing.IsEnabled`. Same check in App's version, but now during App's own ctor. Verify `Testing` is constructed before `SettingsStore` (it is — line 285 vs 292 in today's ctor).
5. **Tests that construct Errors directly OR construct settings stores directly** — sweep the tests carefully. The grep should catch them.
6. **The IStore interface name change** — any place that uses `ISettingsStore` as a parameter or field type needs updating. Grep `ISettingsStore` everywhere.

**Dependencies: none.** Independent of stages 10, 11, 12.

### Tests

**No new tests required** for the behavior shift — observable behavior preserved (`%Settings.X%` resolves the same way; `app.SettingsStore.Get/Set` works the same).

**Existing test coverage to verify:**
- Any test that sets up `app.System.SettingsStore` or constructs a `SettingsVariable` — sweep.
- `PLang.Tests/App/Settings/` if it exists.
- `PLang.Tests/App/Variables/` — `%Settings.X%` resolution.
- `Tests/` — full PLang suite.

### Watch for (coder eyes-on)

- **The `SettingsVariable` reference at App.this.cs:150 doc comment** — search and update.
- **The `Lazy<>` pattern on Actor's SettingsStore** — there's a similar pattern elsewhere (Cache?). Don't generalize the pattern in this stage; just remove from Actor.
- **The `_dataSource.IsValueCreated` check** in Actor's DisposeAsync (around line 175 today) — verify what disposes the now-app-level SettingsStore. App.DisposeAsync should `await SettingsStore.DisposeAsync()` (or similar) to maintain the dispose contract.
- **The `Resources` block at App.this.cs disposal** — after stage 4's dispose-self-owns work, dispose ordering is well-defined. SettingsStore disposes after Modules/Providers/KeepAlive but before App's own teardown — slot it in cleanly.
- **The InMemory connection sharing** for `Testing.IsEnabled` — the SQLite InMemory factory (today's `SqliteSettingsStore.InMemory(name)`) returns a connection whose lifetime depends on a "sentinel connection" (per CLAUDE.md memory I have on the topic). Make sure that sentinel still works at App level — same InMemory mechanism, just constructed once instead of per-actor.
- **Settings.Get path semantics** — when path is empty, today's SettingsVariable.GetChild returns `this`. After: my brief returns `Data.@this.NotFound("Settings")`. Verify which behavior is right by reading the call sites; if any test or PLang code does `%Settings%` (no dot), my brief breaks it. If so, return some shape representing "the Settings root" — maybe an empty Data with name "Settings". But I'd lean: `%Settings%` alone is meaningless; NotFound is fine.

### Stages that follow this one

- Tier 4 stages (14-22) — all independent.
- The deeper "Error.@this.App back-ref drop" (out of scope from stage 11) is still a candidate.

### Out of scope

- Any change to the IStore interface contract (Get/Set/Exists/etc. shape stays).
- Other classes' Settings-adjacent reaches that aren't in the 10-site sweep.
- The `JsonConstructor` round-trip pattern in storage — stays inside Sqlite.cs.

## Commit plan

```
runtime2-cleanup stage 13: Settings reshape — collection-over-Data + app-level SettingsStore + RegisterNavigable

Three tightly-coupled changes that close the SettingsVariable smell
and move SettingsStore to its right scope:

1. Settings.@this becomes a collection-over-Data, not a Data subclass.
   Replaces SettingsVariable (which inherited from Data.@this only to
   fit through GetChild's navigation interface — mechanism leaking
   into shape). New surface: Get(path, context) and Set(key, data).
   Two-method @this. No inheritance.

2. SettingsStore moves from per-actor to app-level.
   - Actor's _dataSource (Lazy<ISettingsStore>), SettingsStore property,
     and CreateSettingsStore method all delete.
   - App gains SettingsStore { get; } backed by system.sqlite.
   - 10 caller sites sweep app.System.SettingsStore → app.SettingsStore
     across Goals/Setup, identity provider, llm provider, settings
     module.

3. Variables.@this gains RegisterNavigable(name, resolver).
   - New private dictionary _navigables; Get checks it before
     returning NotFound.
   - Each actor's Variables registers "Settings" with a resolver
     that delegates to app.Settings.Get(path, Context).
   - Replaces Actor.this.cs:133's
     Context.Variables.Set(app.SettingsVariable.Name, app.SettingsVariable).
   - Generalizable to any future non-Data navigable mount.

Renames:
  ISettingsStore.cs    → IStore.cs
  SqliteSettingsStore.cs → Sqlite.cs
  SettingsVariable.cs  → DELETED (absorbed into Settings/this.cs)

Behavior preserved:
- %Settings.X% resolves identically (load-from-store on first
  segment, navigate Data.GetChild on remainder).
- AskError on unset values (same shape).
- Per-actor Context propagates through the resolver lambda capture.
```
