# In-Memory SQLite DataSource — Coder Handoff

## Problem

All SQLite-backed datasources need an in-memory mode for:
1. **PLang tests** — test runner creates fresh engines, datasources should be ephemeral
2. **Builder** — executes setup goals (CREATE TABLE, etc.) to validate SQL against schema, needs real SQLite but not real files
3. **C# unit tests** — outside PLang runtime's hands, but should be easy to opt into

The in-memory DB must stay alive across operations (so CREATE TABLE in step 1 is visible to SELECT in step 2). SQLite in-memory DBs vanish when the last connection closes, so a sentinel connection must be held open.

## Design

### 1. `SqliteDataSource` — add in-memory construction path

**File:** `PLang/App/DataSource/SqliteDataSource.cs`

Add a static factory method and private constructor for in-memory mode:

```csharp
public static SqliteDataSource InMemory(string name)
    => new SqliteDataSource(name, inMemory: true);
```

Internal changes:
- New field: `private readonly SqliteConnection? _sentinel;`
- Private constructor for in-memory: builds connection string `Data Source={name};Mode=Memory;Cache=Shared;`, opens sentinel connection, skips directory creation and WAL (not needed for in-memory)
- The existing public constructor (`SqliteDataSource(dbPath, fileSystem)`) stays unchanged — production path
- `Dispose()`: close and dispose sentinel before clearing pool

Connection string for in-memory:
```
Data Source={name};Mode=Memory;Cache=Shared;
```

The sentinel opens at construction. All operations use the existing connection-per-operation pattern — they create `new SqliteConnection(_connectionString)`, which connects to the same shared-cache in-memory DB. The sentinel just prevents the DB from being garbage collected.

Lifecycle: sentinel lives as long as DataSource → DataSource lives as long as Actor → Actor lives as long as Engine. `Engine.DisposeAsync()` → `Actor.DisposeAsync()` → `DataSource.Dispose()` → sentinel closes → in-memory DB vanishes.

### 2. `Engine.Building` — new object on Engine

**New file:** `PLang/App/Build/this.cs`

Follow the exact pattern of `PLang/App/Test/this.cs` and `PLang/App/Debug/this.cs`:

```csharp
namespace App.Build;

public sealed class @this
{
    private readonly Engine.@this _engine;

    public bool IsEnabled { get; set; }

    public @this(Engine.@this engine)
    {
        _engine = engine;
    }
}
```

Minimal for now — just `IsEnabled`. The runtime2 builder will add more properties later. The `_engine` back-reference follows the same pattern as Debug and Test.

**Wire into Engine (`PLang/App/this.cs`):**
- Add property: `public Building Building { get; }`
- In constructor: `Building = new Building(this);`

**Add global alias (`PLang/App/GlobalUsings.cs`):**
```csharp
global using Building = App.Build.@this;
```

### 3. `Actor.CreateDataSource()` — navigate and decide

**File:** `PLang/App/Context/Actor.cs`

Change `CreateDataSource()` to check Engine context:

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

Actor navigates to Engine to read context that already exists (Testing, Building). It's not asking "should I use in-memory" — it's reading what mode the engine is in and making its own decision.

### 4. Update existing tests

**Files:**
- `PLang.Tests/App/Modules/datasource/DataSourceTests.cs`
- `PLang.Tests/App/Modules/settings/SettingsDataTests.cs`

These currently create temp directories and real SQLite files. After this change, when tests set `engine.Testing.IsEnabled = true`, the Actor will automatically use in-memory datasources. The tests should:
- Set `engine.Testing.IsEnabled = true` in setup
- Remove temp directory creation/cleanup (no longer needed for datasource tests that go through Actor)
- Tests that test `SqliteDataSource` directly (not through Actor) can continue using temp files OR use `SqliteDataSource.InMemory("test")`

Review each test to determine the right approach — some may intentionally test file-backed behavior.

### 5. Add in-memory DataSource tests

**File:** `PLang.Tests/App/Modules/datasource/DataSourceTests.cs` (extend existing)

Add test cases that verify:
- `SqliteDataSource.InMemory(name)` creates a working datasource
- All CRUD operations work (Get, Set, Remove, Exists, GetAll, Tables)
- Schema persists across operations (CREATE TABLE then SELECT)
- Two datasources with different names are isolated
- Dispose closes sentinel, DB vanishes (new connection to same name returns empty)
- Actor uses in-memory when `Engine.Testing.IsEnabled` is true
- Actor uses in-memory when `Engine.Building.IsEnabled` is true
- Actor uses file-backed when neither is set (default)

## Files to modify

| File | Change |
|---|---|
| `PLang/App/DataSource/SqliteDataSource.cs` | Add `InMemory()` factory, sentinel field, in-memory constructor, update `Dispose()` |
| `PLang/App/Build/this.cs` | **New file** — Building object with `IsEnabled` |
| `PLang/App/this.cs` | Add `Building` property and constructor wiring |
| `PLang/App/GlobalUsings.cs` | Add `Building` global alias |
| `PLang/App/Context/Actor.cs` | Update `CreateDataSource()` to check Testing/Building |
| `PLang.Tests/App/Modules/datasource/DataSourceTests.cs` | Add in-memory tests, update existing if appropriate |

## OBP compliance

- **Navigate, don't pass**: Actor navigates to `Engine.Testing.IsEnabled` and `Engine.Building.IsEnabled` — no flags passed in
- **Behavior belongs to owner**: `SqliteDataSource` manages its own sentinel — no external lifecycle management
- **Names are nouns**: `Building` (not `BuildMode` or `BuildConfig`)
- **Datasource concern stays on datasource**: Engine doesn't know about in-memory. Actor reads context and decides. SqliteDataSource manages the mechanics.

## Out of scope

- PLang test `.goal` files for in-memory datasource (add when the setup.goal execution system is implemented)
- App builder integration (the builder is still runtime1 — this prepares the property for when it migrates)
- C# unit test helpers/utilities (test authors use `SqliteDataSource.InMemory()` directly or set `Testing.IsEnabled`)
