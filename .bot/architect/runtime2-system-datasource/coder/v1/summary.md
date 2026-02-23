# v1 Summary: DataSource + Settings Variable Bridge

## What this is
Implements persistent key-value storage (DataSource) and a Settings variable bridge for Runtime2. This allows PLang users to store and retrieve settings that persist across restarts, and access them via `%Settings.ApiKey%` variable syntax. Each actor (System, Service, User) gets its own SQLite database at `.db/{actorname}.sqlite`.

## What was done

### New Files Created
- **`PLang/Runtime2/Engine/DataSource/IDataSource.cs`** — Interface for persistent key-value storage with Get, GetAll, Set, Remove, Exists, Tables operations. Static `ResolveTableName(Type)` convention.
- **`PLang/Runtime2/Engine/DataSource/SqliteDataSource.cs`** — SQLite implementation with WAL mode, two-column schema (key TEXT PK, data TEXT as JSON), auto table creation, table name sanitization for SQL injection prevention.
- **`PLang/Runtime2/Engine/DataSource/SettingsData.cs`** — Extends Data with virtual GetChild override. Intercepts dot-notation navigation so `%Settings.ApiKey%` calls `DataSource.Get("settings", "ApiKey")` per-key. Missing keys return AskError.
- **`PLang/Runtime2/Engine/Errors/DataSourceError.cs`** — Error type with TableName/KeyName context. FromException factory classifies SQLite errors (locked, corrupt, permission denied).
- **`PLang/Runtime2/Engine/Errors/AskError.cs`** — Error for missing required values, carrying Table and DataKey. Runtime handling (prompt-store-retry) deferred to separate branch.
- **`PLang/Runtime2/actions/settings/get.cs`** — Settings get handler.
- **`PLang/Runtime2/actions/settings/set.cs`** — Settings set handler.
- **`PLang/Runtime2/actions/settings/remove.cs`** — Settings remove handler.
- **`PLang/Runtime2/actions/settings/types.cs`** — Settings result types.

### Files Modified
- **`PLang/Runtime2/Engine/Memory/Data.Navigation.cs`** — Made `GetChild` virtual so SettingsData can override it.
- **`PLang/Runtime2/Engine/Context/Actor.cs`** — Added lazy `DataSource` property, registered `SettingsData` on System actor's MemoryStack, disposed DataSource in DisposeAsync.
- **`PLang.Generators/LazyParamsGenerator.cs`** — Added `__resolutionError` field. Modified `__Resolve<T>` to use `MemoryStack.Get()` instead of `GetValue()` for full-match variables, checking for errors on resolved Data. Added error check before `Run()` in `CodeGeneratedExecuteAsync`.

### Tests
- **`PLang.Tests/Runtime2/Modules/datasource/DataSourceTests.cs`** — 13 tests covering CRUD, null values, integer preservation, table isolation, ResolveTableName.
- **`PLang.Tests/Runtime2/Modules/settings/SettingsDataTests.cs`** — 10 tests covering SettingsData.GetChild, AskError on missing keys, dot-notation via MemoryStack, settings handlers, Actor DataSource lazy creation.
- All 1446 C# tests pass (0 failures).
- All 15 PLang tests that passed before still pass (8 pre-existing failures unrelated to this change).

## Code example

**SettingsData.GetChild** — the key pattern for lazy per-key loading:
```csharp
public override Data? GetChild(string path, int depth = 0)
{
    // Split "ApiKey.SubProp" → key="ApiKey", remaining="SubProp"
    var key = /* first segment */;
    var dataSource = _engine.System.DataSource;
    var result = dataSource.Get("settings", key).GetAwaiter().GetResult();

    if (result.Value == null)
        return FromError(new AskError(
            $"Settings value '{key}' is not set.", "settings", key));

    var child = new Data(key, result.Value, parent: this);
    return string.IsNullOrEmpty(remaining) ? child : child.GetChild(remaining, depth + 1);
}
```

**LazyParamsGenerator error propagation** — catches errors during variable resolution:
```csharp
var __resolved = __memoryStack!.Get(fullMatch.Groups[1].Value);
if (__resolved != null && !__resolved.Success)
{
    __resolutionError = __resolved;
    return default;
}
```
