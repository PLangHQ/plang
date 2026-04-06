# v1 Summary — In-Memory SQLite DataSource

## What this is

App needs in-memory SQLite datasources for three consumers: PLang tests, the builder (which executes SQL to validate schema), and C# unit tests. The in-memory DB must be real SQLite (not a dictionary) because the builder runs actual CREATE TABLE / SELECT statements. A sentinel connection pattern keeps the in-memory DB alive across connection-per-operation calls.

## What was done

Designed the in-memory datasource architecture through iterative discussion. Three key design decisions from Ingi:

1. **SQLite, not dictionary** — the builder validates real SQL against real schema, so the datasource must be SQLite in-memory, not a simple key-value dictionary.
2. **Engine doesn't know about in-memory** — that's a datasource concern. No `Engine.InMemory` flag.
3. **Signal flows through existing context** — `Engine.Testing.IsEnabled` already exists. A new `Engine.Building.IsEnabled` follows the same pattern. Actor navigates to these to decide.

## Key decisions

- `SqliteDataSource.InMemory(name)` — static factory, sentinel connection opens at construction, closes at Dispose
- `App.Build.@this` — new file, follows Debug/Test pattern, global alias `Building`
- `Actor.CreateDataSource()` checks `Engine.Testing.IsEnabled || Engine.Building.IsEnabled`
- C# unit tests handle it themselves — either set `Testing.IsEnabled` or use `SqliteDataSource.InMemory()` directly

## Files to modify

- `PLang/App/DataSource/SqliteDataSource.cs` — add InMemory factory + sentinel
- `PLang/App/Build/this.cs` — new file
- `PLang/App/this.cs` — add Building property
- `PLang/App/GlobalUsings.cs` — add Building alias
- `PLang/App/Context/Actor.cs` — update CreateDataSource()
- `PLang.Tests/App/Modules/datasource/DataSourceTests.cs` — add in-memory tests

## Code example

```csharp
// Actor navigates to Engine context, makes its own decision
private IDataSource CreateDataSource()
{
    if (Engine.Testing.IsEnabled || Engine.Building.IsEnabled)
        return SqliteDataSource.InMemory(Name.ToLowerInvariant());

    var dbDir = Engine.FileSystem.Path.Combine(Engine.AbsolutePath, ".db");
    var dbPath = Engine.FileSystem.Path.Combine(dbDir, $"{Name.ToLowerInvariant()}.sqlite");
    return new SqliteDataSource(dbPath, Engine.FileSystem);
}
```
