# Stage 13 — coder plan (`settings-collection-rework`)

## What

Three tightly-coupled changes:

1. `Settings.@this` becomes a collection-over-Data, not a Data subclass.
   Replaces `SettingsVariable` (which inherited from `Data.@this` only to
   intercept `%Settings.X%` via `GetChild` override).
2. `SettingsStore` moves from per-actor to app-level. User's store had
   zero consumers; per-actor allocation was dead drift.
3. `Variables.@this.RegisterNavigable(name, resolver)` — generalizable
   hook for non-Data navigable mounts. Settings registers per-actor.

Plus renames:
- `Settings/ISettingsStore.cs` → `Settings/IStore.cs`
- `Settings/SqliteSettingsStore.cs` → `Settings/Sqlite.cs`
- `Settings/SettingsVariable.cs` → DELETED (absorbed into `Settings/this.cs`)

## File map

### New / changed in PLang/App/Settings/

- `IStore.cs` — interface renamed; class `IStore : IDisposable`. `ResolveTableName` static method preserved.
- `Sqlite.cs` — class `Sqlite : IStore`. `Sqlite.InMemory(name)` factory.
- `this.cs` — NEW. `Settings.@this` with private `_app` field; `Get(path, context)` and `Set(key, data)`. Two-method surface, no inheritance.

### Modified

- `PLang/App/this.cs`:
  - Drop `internal SettingsVariable SettingsVariable { get; }` (line 154).
  - Add `public IStore SettingsStore { get; }` and `public Settings.@this Settings { get; }`.
  - In ctor: drop `SettingsVariable = new SettingsVariable(this);`; allocate `SettingsStore = CreateSettingsStore();` and `Settings = new Settings.@this(this);`.
  - New `private IStore CreateSettingsStore()` (logic moved from `Actor.CreateSettingsStore`, hardcoded "system" name).
  - DisposeAsync: dispose SettingsStore.
- `PLang/App/Actor/this.cs`:
  - Drop `_dataSource`, `SettingsStore` property, `CreateSettingsStore` method.
  - Replace `Context.Variables.Set(app.SettingsVariable.Name, app.SettingsVariable);` with `Context.Variables.RegisterNavigable("Settings", path => app.Settings.Get(path, Context));`.
  - Drop `_dataSource.Value.Dispose()` from DisposeAsync.
- `PLang/App/Variables/this.cs`:
  - New `_navigables` dictionary + `RegisterNavigable(name, resolver)` method.
  - `Get` checks `_navigables` before returning NotFound.
  - Drop `is App.Settings.SettingsVariable` checks in Clone/Snapshot — dead branches (SettingsVariable no longer in `_variables`).
- `PLang/App/Variables/this.Snapshot.cs`:
  - Drop SettingsVariable type check (dead).
- `PLang/App/Data/this.Navigation.cs:252` — comment refresh ("e.g., DynamicData" — drop SettingsVariable mention).

### Caller sweep (10 production sites)

- `PLang/App/Goals/Setup/this.cs:108, 143` — `app.System.SettingsStore` → `app.SettingsStore`.
- `PLang/App/modules/identity/providers/DefaultIdentityProvider.cs:207, 226, 277, 285` — `action.Context.App.System.SettingsStore` → `action.Context.App.SettingsStore`.
- `PLang/App/modules/llm/providers/OpenAiProvider.cs:58, 837` — `app.System.SettingsStore` → `app.SettingsStore`; `ISettingsStore` → `IStore`.
- `PLang/App/modules/settings/get.cs:20, 21` — store path; `Get<SettingsVariable>` → `Get<Data.@this>` (or non-generic).
- `PLang/App/modules/settings/remove.cs:17` — store path.
- `PLang/App/modules/settings/set.cs:19, 20` — store path; `new SettingsVariable(...)` → `new Data.@this(...)`.

### Test sweep

- `PLang.Tests/App/Goals/Setup/SetupTests.cs` — 3 sites: `_app.System.SettingsStore` → `_app.SettingsStore`.
- `PLang.Tests/App/Modules/datasource/DataSourceTests.cs` — `SqliteSettingsStore` → `Sqlite`; `ISettingsStore.ResolveTableName` → `IStore.ResolveTableName`.
- `PLang.Tests/App/Modules/identity/IdentityErrorPathTests.cs` — `ISettingsStore` → `IStore`; `SwapDataSource` retargeted from Actor's private `_dataSource` to App's SettingsStore property; 11 sites of `_app.System.SettingsStore`.
- `PLang.Tests/App/Modules/settings/SettingsDataTests.cs` — `new SettingsVariable(...)` → `new Data.@this(...)`; `_app.System.SettingsStore` → `_app.SettingsStore`. The `SettingsData_SameObjectAcrossAllActors` test re-purposes (or deletes) — Settings is no longer in `_variables`; the resolver-lambda mechanism doesn't return a shared object. Replace with a test that asserts `app.Settings` is the same instance from any context.
- `PLang.Tests/App/VariablesTests/VariablesSnapshotTests.cs` — drop the `is SettingsVariable` assertion (dead — type doesn't exist).
- `PLang.Tests/App/Context/ActorSettingsStoreTests.cs` — `User.SettingsStore` is gone. Tests re-purposed to assert app-level behavior, or partially deleted (User-specific assertions become invalid). Likely rewrite as `app.SettingsStore` tests.

## Verification

- `find PLang/App/Settings -name 'SettingsVariable.cs'` → empty.
- `grep -rn "SettingsVariable\b" PLang/ PLang.Tests/ Tests/ --include='*.cs'` → 0.
- `grep -rn "app\.System\.SettingsStore\|\.System\.SettingsStore" PLang/ PLang.Tests/ --include='*.cs'` → 0.
- `grep -rn "actor\.SettingsStore\|_dataSource" PLang/App/Actor/this.cs` → 0.
- `grep -rn "ISettingsStore\b" PLang/ PLang.Tests/ --include='*.cs'` → 0.
- `app.SettingsStore` and `app.Settings` exist; `Variables.@this.RegisterNavigable` exists.
- C# 2755/2755 (or new baseline if test rewrites change count); PLang 199/199; build clean.
