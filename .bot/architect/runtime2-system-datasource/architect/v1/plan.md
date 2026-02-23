# Plan: System DataSource + Settings Variable Bridge

## What This Is

Persistent key-value storage for Runtime2 actors, replacing v1's `system.sqlite`. Each actor (System/Service/User) owns its own SQLite database under `.db/`. Modules own their own tables (encryption stores in "encryption", nostr in "nostr", etc.). The system actor's "settings" table is bridged to PLang variable syntax so `%Settings.ApiKey%` resolves lazily from DataSource.

## Files to Create

### 1. `Engine/DataSource/IDataSource.cs`
Interface for persistent storage. Methods: `Get`, `GetAll`, `Set`, `Remove`, `Exists`, `Tables`. All return `Data`, never throw. Table name derived from `System.Type` via static `ResolveTableName` (last namespace segment, lowercased).

### 2. `Engine/DataSource/SqliteDataSource.cs`
Default SQLite implementation. Two-column schema per table: `key TEXT PK`, `data TEXT` (serialized Data JSON). WAL mode. Tables auto-created on first write. Connection per operation (SQLite pools internally). **Must use `IPLangFileSystem`** for directory creation — never `System.IO`. SQLite manages its own file access via connection string, but any directory/path operations go through the abstraction.

### 3. `Engine/Errors/DataSourceError.cs`
Extends `Error`. Captures `TableName` and `KeyName` for diagnostics. `FromException` factory with `FixSuggestion` for common SQLite failures (locked, disk full, corrupt, permissions).

### 4. `Engine/Errors/AskError.cs`
Extends `Error`. Carries `Table` and `Key` — identifies where a missing required value should be stored once provided. Message is the user-facing prompt. Runtime handling of AskError (prompt-store-retry) is out of scope — comes in a separate branch.

### 5. `Engine/DataSource/SettingsData.cs`
Extends `Data`. Overrides `GetChild` to do per-key lazy loading from `engine.System.DataSource`. On access of `%Settings.ApiKey%`:
- Calls `DataSource.Get(module, "ApiKey")` — one row, one read
- If value exists, returns it
- If value is null, returns `Data.FromError(new AskError(...))`
- `.GetAwaiter().GetResult()` is safe because Microsoft.Data.Sqlite is synchronous under the hood (SQLite has no async I/O)

Registered on `engine.System.Context.MemoryStack` as `"Settings"` during actor initialization.

## Files to Modify

### 6. `Engine/Memory/Data.Navigation.cs`
One-word change: `public Data? GetChild(...)` → `public virtual Data? GetChild(...)`. This lets SettingsData (and future specialized Data subclasses) own their navigation behavior.

### 7. `Engine/Context/Actor.cs`
Add lazy `DataSource` property: `_dataSource ??= CreateDataSource()`. `CreateDataSource` resolves path via `IPLangFileSystem` to `.db/{actorname}.sqlite`. Dispose updated to dispose `_dataSource` if created. Register `SettingsData` on `Context.MemoryStack` during initialization.

### 8. `PLang.Generators/LazyParamsGenerator.cs` — Error Propagation Fix
The generated `__Resolve<T>` currently calls `__memoryStack.GetValue(variableName)` which extracts `.Value` from the resolved Data — losing any error (including AskError). The full-match resolution path needs to change:

**Current** (line ~282):
```csharp
return (T?)TypeMapping.ConvertTo(
    __memoryStack!.GetValue(fullMatch.Groups[1].Value), typeof(T));
```

**Should become** (conceptually):
```csharp
var __resolved = __memoryStack!.Get(fullMatch.Groups[1].Value);
if (__resolved != null && !__resolved.Success)
{
    __resolutionError = __resolved;  // stash the error
    return default;
}
return (T?)TypeMapping.ConvertTo(__resolved?.Value, typeof(T));
```

Then in `CodeGeneratedExecuteAsync`, after property access triggers resolution, check `__resolutionError` before calling `Run()`:
```csharp
if (__resolutionError != null) return __resolutionError;
```

This is a general improvement — any variable that resolves to an error-bearing Data (not just Settings) short-circuits the handler. The handler never runs; the error propagates. This is KPR: the resolution told you something, don't ignore it.

**Note**: The interpolation path (`"Hello %Settings.ApiKey% world"`) also needs the same check. If any `%var%` in an interpolated string resolves to an error, that error should propagate.

### 9. NuGet: `Microsoft.Data.Sqlite`
Add to PLang project if not already present.

## Design Decisions (settled)

| Decision | Choice | Why |
|----------|--------|-----|
| Storage granularity | One row per key | Security (only load what's needed), simplicity |
| Settings bridge | SettingsData with virtual GetChild | OBP: Settings owns its navigation, per-key lazy load |
| Async bridge | `.GetAwaiter().GetResult()` | SQLite has no async I/O — its async methods complete synchronously |
| Cache | None | SQLite reads are sub-ms; DynamicData/SettingsData re-reads on each access = always fresh, no invalidation |
| AskError on missing | Both paths | `%Settings.X%` and `get settings 'X'` both return AskError when key doesn't exist |
| AskError handling | Out of scope | Prompt-store-retry runtime loop comes in a separate branch |
| System.IO | Never | `IPLangFileSystem` for all path/directory operations. SQLite connection string is a raw string path — that's SQLite's concern |
| Settings actor | Always System | `%Settings.X%` reads from `engine.System.DataSource`, always |

## PLang User Experience

**Read (transparent):**
```
- post http://api.example.com, bearer: %Settings.ApiKey%
```

**Read (explicit, with AskError):**
```
- get settings 'ApiKey', write to %apiKey%
```

**Set:**
```
- set settings 'ApiKey' = 'sk-123...'
```

**Remove:**
```
- remove settings 'ApiKey'
```

## Out of Scope

- AskError runtime handling (prompt-store-retry loop) — separate branch
- Encrypted DataSource implementation — future, behind `IDataSource` interface
- User/Service actor DataSource usage — future, structure is ready
- Settings PLang action handlers (`actions/settings/get`, `set`, `remove`) — coder work, follows standard handler pattern
