# v1 Summary — ISettings Type Skeletons & Failing Tests

## What this is

Type skeletons and failing tests for a strongly typed, goal-scoped module settings system in PLang App. The architect designed a system where modules declare `ISettings` classes and the source generator handles scope-aware resolution. This scaffolding defines the exact types, signatures, and file placements — the contract the coder works against.

## What was done

### Skeletons created (compilable, empty — `throw new NotImplementedException()`)

| File | Purpose |
|------|---------|
| `PLang/App/Engine/Settings/ISettings.cs` | Marker interface for source generator detection |
| `PLang/App/Engine/Settings/this.cs` | `@this` — registry, resolution logic, `For<T>()`, `Resolve<T>()`, `Set()` |
| `PLang/App/Engine/Settings/Scope.cs` | Key-value store for one goal level (`Get`, `Set`, `Contains`) |
| `PLang/App/Engine/Settings/ModuleView.cs` | `ModuleView<T>` — context-bound view with `Resolve<TValue>()` |
| `PLang/App/actions/archive/Settings.cs` | First use case: `archive.Settings : ISettings` with `Max` (100MB), `Level` (Optimal) |
| `PLang/App/actions/archive/types.cs` | Result type `settingsResult` |

**Note:** The `[Action("settings")]` handler was removed from source — it will be produced by the source generator from `archive.Settings`.

### Existing files modified

| File | Change |
|------|--------|
| `PLang/App/Engine/this.cs` | Added `Settings` property + initialization in constructor |
| `PLang/App/Engine/Context/PLangContext.cs` | Added `SettingsScope` property (nullable `Scope`) |
| `PLang/App/Engine/Goals/Goal/Methods.cs` | Save/restore `SettingsScope` in `RunAsync` try/finally |
| `PLang/App/GlobalUsings.cs` | Added `EngineSettings` and `SettingsScope` aliases |

### Failing tests created

| File | Tests |
|------|-------|
| `PLang.Tests/App/Engine/Settings/ScopeTests.cs` | 5 tests: set/get, null when missing, contains, case-insensitive |
| `PLang.Tests/App/Engine/Settings/SettingsTests.cs` | 6 tests: class default, goal scope, parent inheritance, engine default, priority, child override |
| `PLang.Tests/App/Engine/Settings/ModuleViewTests.cs` | 4 tests: returns view, class default, goal scope, thread safety |
| `Tests/App/Settings/SetMaxGzipSize/Start.test.goal` | PLang integration test |

## Key design decisions (departures from architect)

1. **No `Module<T>()` on Engine** — premature. Used `engine.Settings.For<archive.Settings>(context)` instead. Long-term this may become `engine.Action<archive.@this>().Settings.Max` — see todos.md.

2. **Settings behavior on `Engine.Settings`, not PLangContext** — OBP: behavior belongs to the owner. PLangContext stores raw `Scope` data; `Engine.Settings` owns resolution chain logic.

3. **`IsDefault` not `Default`** — avoids C# keyword collision (architect flagged this as open question).

4. **`archive.Settings` not `ArchiveSettings`** — namespace carries the context, no need to repeat. Renamed from `ArchiveSettings` after review.

5. **Action handler removed from source** — the `[Action("settings")]` handler will be source-generated from `archive.Settings`. A reference copy exists in `.bot/` skeletons showing the target shape.

## Code example

The core pattern — what the coder implements:

```csharp
// Engine.Settings.Resolve walks: context.SettingsScope → parent → Defaults → classDefault
public T Resolve<T>(string key, PLangContext context, T classDefault)
{
    // Walk context chain
    var current = context;
    while (current != null)
    {
        if (current.SettingsScope?.Contains(key) == true)
            return (T)current.SettingsScope.Get(key)!;
        current = current.Parent;
    }
    // Engine defaults
    if (Defaults.Contains(key))
        return (T)Defaults.Get(key)!;
    // Class default
    return classDefault;
}
```

## Build verification

- `PLang.csproj`: builds clean (0 new errors)
- `PLang.Tests.csproj`: 148 pre-existing errors, 0 from my files
- All 15 tests compile and will fail (NotImplementedException) — red phase complete

## What's next for the coder

1. Implement `Scope.Get/Set/Contains` (trivial — ConcurrentDictionary wrapper)
2. Implement `Settings.Resolve<T>()` (walk context chain — see code example above)
3. Implement `Settings.Set()` (lazy-init context.SettingsScope, write to it or to Defaults if isDefault)
4. Implement `Settings.For<T>()` (create ModuleView with module prefix derived from T's namespace)
5. Implement `ModuleView.Resolve<TValue>()` (delegate to Settings.Resolve with prefixed key)
6. Make all 15 C# tests green
7. Source generator (Phase 2) — detect `ISettings` classes, generate:
   - Scope-aware property bodies (read side): `public long Max => context.Engine.Settings.Resolve<long>("archive.max", context, 100 * 1024 * 1024);`
   - `[Action("settings")]` handler (write side): nullable props, `IsDefault` flag, `Run()` writes non-null props to scope
   - Reference for target handler shape: `.bot/runtime2-settings/scaffolder/v1/skeletons/archive_settings.cs`
