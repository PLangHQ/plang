# v1 Summary — ISettings Type Skeletons & Failing Tests

## What this is

Type skeletons and failing tests for a strongly typed, goal-scoped module settings system in PLang Runtime2. The architect designed a system where modules declare `ISettings` classes and the source generator handles scope-aware resolution. This scaffolding defines the exact types, signatures, and file placements — the contract the coder works against.

## What was done

### Skeletons created (compilable, empty — `throw new NotImplementedException()`)

| File | Purpose |
|------|---------|
| `PLang/Runtime2/Engine/Settings/ISettings.cs` | Marker interface for source generator detection |
| `PLang/Runtime2/Engine/Settings/this.cs` | `@this` — registry, resolution logic, `For<T>()`, `Resolve<T>()`, `Set()` |
| `PLang/Runtime2/Engine/Settings/Scope.cs` | Key-value store for one goal level (`Get`, `Set`, `Contains`) |
| `PLang/Runtime2/Engine/Settings/ModuleView.cs` | `ModuleView<T>` — context-bound view with `Resolve<TValue>()` |
| `PLang/Runtime2/actions/archive/ArchiveSettings.cs` | First use case: `Max` (100MB), `Level` (Optimal) |
| `PLang/Runtime2/actions/archive/settings.cs` | `[Action("settings")]` handler with nullable props + `IsDefault` |
| `PLang/Runtime2/actions/archive/types.cs` | Result type `settingsResult` |

### Existing files modified

| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/this.cs` | Added `Settings` property + initialization in constructor |
| `PLang/Runtime2/Engine/Context/PLangContext.cs` | Added `SettingsScope` property (nullable `Scope`) |
| `PLang/Runtime2/Engine/Goals/Goal/Methods.cs` | Save/restore `SettingsScope` in `RunAsync` try/finally |
| `PLang/Runtime2/GlobalUsings.cs` | Added `EngineSettings` and `SettingsScope` aliases |

### Failing tests created

| File | Tests |
|------|-------|
| `PLang.Tests/Runtime2/Engine/Settings/ScopeTests.cs` | 5 tests: set/get, null when missing, contains, case-insensitive |
| `PLang.Tests/Runtime2/Engine/Settings/SettingsTests.cs` | 6 tests: class default, goal scope, parent inheritance, engine default, priority, child override |
| `PLang.Tests/Runtime2/Engine/Settings/ModuleViewTests.cs` | 4 tests: returns view, class default, goal scope, thread safety |
| `Tests/Runtime2/Settings/SetMaxGzipSize/Start.test.goal` | PLang integration test |

## Key design decisions (departures from architect)

1. **No `Module<T>()` on Engine** — premature. Used `engine.Settings.For<ArchiveSettings>(context)` instead. When a Module system arrives, it can delegate.

2. **Settings behavior on `Engine.Settings`, not PLangContext** — OBP: behavior belongs to the owner. PLangContext stores raw `Scope` data; `Engine.Settings` owns resolution chain logic.

3. **`IsDefault` not `Default`** — avoids C# keyword collision (architect flagged this as open question).

4. **File naming: `ArchiveSettings.cs`** — avoids case collision with `settings.cs` (action handler) on case-insensitive filesystems.

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
2. Implement `Settings.Resolve<T>()` (walk context chain)
3. Implement `Settings.Set()` (lazy-init context.SettingsScope)
4. Implement `Settings.For<T>()` (create ModuleView with module prefix)
5. Implement `ModuleView.Resolve<TValue>()` (delegate to Settings.Resolve with prefix)
6. Implement `archive/settings.cs Run()` (write non-null props to scope via Engine.Settings.Set)
7. Make all 15 C# tests green
8. Source generator changes (Phase 2) — detect ISettings, generate scope-aware properties
