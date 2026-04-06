# Security v1 Summary — runtime2-inmemory-datasource

## What this is

Security audit (blue team + red team) of the in-memory SQLite datasource feature. This branch adds `SqliteDataSource.InMemory()` with a sentinel connection pattern, a `Building` mode controller on Engine, settings action handlers (`get`/`set`/`remove`), and `SettingsData` — a `Data` subclass that lazily loads settings from the System actor's DataSource on property access.

## What was done

**Phase 1 (Blue Team):** Mapped 6 attack surface areas — in-memory DB isolation, sentinel lifecycle, DeserializeValue exception handling, SQL injection surface, SettingsData navigation, Building.IsEnabled public setter.

**Phase 2 (Red Team):** Assessed exploitability of each area under PLang's user-sovereign threat model.

**Key findings:**

1. **Medium (carry-forward): DeserializeValue InvalidOperationException gap** — `SqliteDataSource.cs:282-296` catches `JsonException` but not `InvalidOperationException` from `Data.UnwrapJsonElement`'s depth guard. **Currently unreachable** because `JsonDocument.Parse` default MaxDepth=64 rejects deep JSON before the 128-depth guard fires. Still violates the "behavior methods never throw" contract. Fix: add `catch (InvalidOperationException)` returning raw string.

2. **Low: Use-after-dispose** — CRUD methods don't check `_disposed`. Not exploitable: resulting `SqliteException` is caught by the method's own handler.

3. **Low (accepted): In-memory DB name collision** — Process-wide shared cache. Two engines with `InMemory("system")` share state. No current code path creates parallel engines.

4. **Low (accepted): sync-over-async** — `SettingsData.GetChild` uses `.GetAwaiter().GetResult()`. Safe for SQLite, constrains future async IDataSource implementations.

**Verdict: PASS** — no critical or high findings.

## Code example

The carry-forward finding pattern (SqliteDataSource.cs:282-296):
```csharp
private static object? DeserializeValue(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json); // MaxDepth=64 → JsonException (caught)
        return Data.UnwrapJsonElement(doc.RootElement); // MaxDepth=128 → InvalidOperationException (NOT caught)
    }
    catch (JsonException) // ← only catches one of two possible exceptions
    {
        return json;
    }
    // Missing: catch (InvalidOperationException) { return json; }
}
```

## Files reviewed

- `PLang/App/Engine/DataSource/SqliteDataSource.cs` — InMemory factory, sentinel, CRUD, DeserializeValue
- `PLang/App/Engine/DataSource/SettingsData.cs` — GetChild override, depth propagation
- `PLang/App/Engine/DataSource/IDataSource.cs` — interface contract
- `PLang/App/Engine/Build/this.cs` — Building mode controller
- `PLang/App/Engine/Context/Actor.cs` — CreateDataSource routing
- `PLang/App/Engine/this.cs` — Building property, actor lazy init
- `PLang/App/Engine/Errors/DataSourceError.cs`, `AskError.cs` — error types
- `PLang/App/actions/settings/get.cs`, `set.cs`, `remove.cs` — settings handlers
- `PLang/App/Engine/Memory/Data.cs` — UnwrapJsonElement depth guard
- `PLang/App/Engine/Memory/Data.Navigation.cs` — GetChild depth guard
- `PLang/App/Engine/Memory/Variables.cs` — Clone, SettingsData preservation
