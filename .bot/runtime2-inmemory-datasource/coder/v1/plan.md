# In-Memory SQLite DataSource — Coder v1 Plan

## Goal

Implement the architect's design from `.bot/runtime2-inmemory-datasource/architect/v1/plan.md`:
in-memory SQLite datasource support for PLang tests, builder, and C# unit tests.

## Changes (in order)

### 1. `SqliteDataSource.InMemory(name)` — static factory + sentinel

**File**: `PLang/App/DataSource/SqliteDataSource.cs`

- Add `private readonly SqliteConnection? _sentinel;` field
- Add private constructor for in-memory mode:
  - Connection string: `Data Source={name};Mode=Memory;Cache=Shared;`
  - Opens sentinel connection immediately
  - Skips directory creation and WAL
- Add static factory: `public static SqliteDataSource InMemory(string name)`
- Update `Dispose()`: close and dispose sentinel before clearing pool

### 2. `Engine/Build/this.cs` — new file

**New file**: `PLang/App/Build/this.cs`

Follows Debug/Test pattern exactly:
```csharp
namespace App.Build;
public sealed class @this
{
    private readonly Engine.@this _engine;
    public bool IsEnabled { get; set; }
    public @this(Engine.@this engine) { _engine = engine; }
}
```

### 3. Wire `Building` into Engine

**File**: `PLang/App/this.cs`

- Add property: `public Building Building { get; }` (after Testing)
- In constructor: `Building = new Building(this);` (after Testing init)

### 4. Add global alias

**File**: `PLang/App/GlobalUsings.cs`

- Add: `global using Building = App.Build.@this;` (after Testing alias)

### 5. Update `Actor.CreateDataSource()`

**File**: `PLang/App/Context/Actor.cs`

Navigate to Engine.Testing.IsEnabled and Engine.Building.IsEnabled. If either is true, use `SqliteDataSource.InMemory(Name.ToLowerInvariant())`. Otherwise, file-backed path unchanged.

### 6. Tests

**File**: `PLang.Tests/App/Modules/datasource/DataSourceTests.cs` (extend)

Add 7 new tests:
- `InMemory_CrudOperations` — Set/Get/Remove/Exists/GetAll work
- `InMemory_SchemaPersistsAcrossOperations` — CREATE TABLE visible to subsequent SELECT
- `InMemory_TwoNamesAreIsolated` — different names = different DBs
- `InMemory_DisposeClosesDb` — after Dispose, new InMemory with same name starts empty
- `Actor_UsesInMemory_WhenTestingEnabled` — Engine.Testing.IsEnabled → in-memory
- `Actor_UsesInMemory_WhenBuilderEnabled` — Engine.Builder.IsEnabled → in-memory (note: property name is Building, not Builder)
- `Actor_UsesFileBacked_ByDefault` — neither flag → file on disk

Existing tests stay file-backed.

## Verification

1. `dotnet build PLang/PLang.csproj` — 0 errors
2. `dotnet test PLang.Tests` — all tests pass
