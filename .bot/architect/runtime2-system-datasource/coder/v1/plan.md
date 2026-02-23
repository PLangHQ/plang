# Coder Plan v1: Implement DataSource + Settings Bridge

## Overview
Implement the architect's plan for persistent key-value storage (DataSource) and Settings variable bridge for Runtime2.

## Files to Create

### 1. `PLang/Runtime2/Engine/DataSource/IDataSource.cs`
- Interface: `Get`, `GetAll`, `Set`, `Remove`, `Exists`, `Tables`
- All return `Task<Data>`, never throw
- Static `ResolveTableName(Type)` — last namespace segment, lowercased

### 2. `PLang/Runtime2/Engine/DataSource/SqliteDataSource.cs`
- SQLite implementation with WAL mode
- Two-column schema: `key TEXT PK`, `data TEXT` (JSON-serialized Data value)
- Auto-create tables on first write
- Use `IPLangFileSystem` for directory creation, SQLite manages file access
- Dispose pattern for connection cleanup

### 3. `PLang/Runtime2/Engine/Errors/DataSourceError.cs`
- Extends `Error`, captures `TableName` and `KeyName`
- `FromException` factory with SQLite-specific fix suggestions

### 4. `PLang/Runtime2/Engine/Errors/AskError.cs`
- Extends `Error`, carries `Table` and `Key`
- Message = user-facing prompt for missing value
- Runtime handling (prompt-store-retry) is out of scope

### 5. `PLang/Runtime2/Engine/DataSource/SettingsData.cs`
- Extends `Data`, overrides `GetChild` for per-key lazy loading
- On `%Settings.ApiKey%`: calls `DataSource.Get("settings", "ApiKey")`
- Missing key → returns `Data.FromError(new AskError(...))`
- Registered on `engine.System.Context.MemoryStack` as `"Settings"`

## Files to Modify

### 6. `PLang/Runtime2/Engine/Memory/Data.Navigation.cs`
- Make `GetChild` virtual: `public virtual Data? GetChild(...)`

### 7. `PLang/Runtime2/Engine/Context/Actor.cs`
- Add lazy `DataSource` property
- Path: `.db/{actorname}.sqlite`
- Register `SettingsData` on System actor's MemoryStack
- Dispose DataSource if created

### 8. `PLang.Generators/LazyParamsGenerator.cs`
- Add `__resolutionError` field
- In `__Resolve<T>`: check if resolved Data has error, stash in `__resolutionError`
- In `CodeGeneratedExecuteAsync`: check `__resolutionError` before calling `Run()`
- Same check for interpolated strings

### 9. Settings action handlers
- `PLang/Runtime2/actions/settings/get.cs` — get settings value
- `PLang/Runtime2/actions/settings/set.cs` — set settings value
- `PLang/Runtime2/actions/settings/remove.cs` — remove settings value
- `PLang/Runtime2/actions/settings/types.cs` — result types

### 10. `PLang/Runtime2/GlobalUsings.cs`
- Add alias for DataSource types if needed

## Tests

### C# Tests
- `PLang.Tests/Runtime2/Modules/datasource/DataSourceTests.cs` — SqliteDataSource CRUD
- `PLang.Tests/Runtime2/Modules/settings/SettingsDataTests.cs` — SettingsData.GetChild

### PLang Tests
- `Tests/Runtime2/Settings/Settings.test.goal` — full pipeline test

## Build Order
1. Create error types (DataSourceError, AskError)
2. Create IDataSource + SqliteDataSource
3. Make GetChild virtual
4. Create SettingsData
5. Modify Actor.cs
6. Create settings handlers
7. Modify LazyParamsGenerator
8. Build and fix errors
9. Write and run tests
