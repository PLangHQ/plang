# In-Memory SQLite DataSource — Coder v1 Summary

## What this is

In-memory SQLite datasource support for PLang Runtime2. When `Engine.Testing.IsEnabled` or `Engine.Building.IsEnabled` is true, actors automatically use ephemeral in-memory SQLite databases instead of file-backed ones. This enables clean test isolation and lets the builder validate SQL against real schema without creating files on disk.

## What was done

Implemented the architect's design from `.bot/runtime2-inmemory-datasource/architect/v1/plan.md`:

### Files modified

- **`PLang/Runtime2/Engine/DataSource/SqliteDataSource.cs`** — Added `InMemory(string name)` static factory method, private in-memory constructor with sentinel connection (`_sentinel` field), updated `Dispose()` to close sentinel before clearing pool.

- **`PLang/Runtime2/Engine/Build/this.cs`** — New file. `Building` object following the Debug/Test pattern (`IsEnabled` property, engine back-reference).

- **`PLang/Runtime2/Engine/this.cs`** — Added `Building` property (`Build.@this` type) and constructor init.

- **`PLang/Runtime2/GlobalUsings.cs`** — Added comment documenting that `Building` can't be a global alias due to v1 `PLang.Building` namespace conflict. Same pattern as Engine and CallStack.

- **`PLang/Runtime2/Engine/Context/Actor.cs`** — Updated `CreateDataSource()` to navigate to `Engine.Testing.IsEnabled || Engine.Building.IsEnabled` and use `SqliteDataSource.InMemory()` when either is true.

- **`PLang.Tests/Runtime2/Modules/datasource/DataSourceTests.cs`** — Added 7 new tests.

### Key decision: no global alias for Building

The `PLang.Building` namespace (v1 builder) conflicts with a `Building` global alias. This follows the same pattern as `Engine` and `CallStack` — documented in GlobalUsings.cs with the comment explaining how to reference it from different scopes.

## Code example

The core change — `SqliteDataSource.InMemory()`:

```csharp
private SqliteDataSource(string name, bool inMemory)
{
    _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = name,
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared
    }.ToString();

    _sentinel = new SqliteConnection(_connectionString);
    _sentinel.Open();
}

public static SqliteDataSource InMemory(string name)
    => new SqliteDataSource(name, inMemory: true);
```

And `Actor.CreateDataSource()`:

```csharp
private IDataSource CreateDataSource()
{
    if (Engine.Testing.IsEnabled || Engine.Building.IsEnabled)
        return SqliteDataSource.InMemory(Name.ToLowerInvariant());

    var dbDir = Engine.FileSystem.Path.Combine(Engine.AbsolutePath, ".db");
    var dbPath = Engine.FileSystem.Path.Combine(dbDir, $"{Name.ToLowerInvariant()}.sqlite");
    return new SqliteDataSource(dbPath, Engine.FileSystem);
}
```

## Verification

- `dotnet build PLang/PLang.csproj` — 0 errors
- Full test suite: 1472 passed, 0 failed (1465 existing + 7 new)
