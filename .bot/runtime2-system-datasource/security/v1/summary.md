# Security Audit v1 — runtime2-system-datasource

## What this is

Security audit of the DataSource persistence layer and Settings variable bridge introduced on `runtime2-system-datasource`. This branch adds SQLite-backed key-value storage per actor, a `SettingsData` class that bridges `%Settings.X%` variable resolution to database reads, and PLang action handlers for settings CRUD.

## What was done

**Phase 1 (Blue Team):** Mapped 8 attack surface areas — SQL injection surface, JSON deserialization paths, sync-over-async patterns, actor name → file path construction, error propagation in generated code, migration races, resource exhaustion, and clone semantics.

**Phase 2 (Red Team):** Attempted to construct attack scenarios for each surface. Key findings:

### Findings

| ID | Severity | Area | Issue |
|----|----------|------|-------|
| 1 | **Medium** | DeserializeValue | Catches `JsonException` but not `InvalidOperationException` from `UnwrapJsonElement` depth guard. Exception propagates uncaught through `SettingsData.GetChild`. |
| 2 | Low | SqliteDataSource | No use-after-dispose guard. `_disposed` set but never checked in CRUD methods. |
| 3 | Low | SqliteDataSource | `EnsureTable` called on every operation — redundant DDL on hot paths. |
| 4 | Low | SqliteSettingsRepository | TOCTOU race in Settings→SettingsV1 table rename migration. |
| 5 | Low (accepted) | SettingsData | Sync-over-async safe for SQLite but constrains future IDataSource implementations. |
| 6 | By-design | SqliteDataSource | No app-level storage limits — user-sovereign. |

### What's solid

- **SQL injection: properly mitigated.** Table names sanitized (alphanumeric + underscore only), keys always parameterized.
- **Path traversal: not possible.** Actor names hardcoded to system/service/user in `Engine.GetActor()` switch.
- **Error propagation: correct.** `__resolutionError` in generated code properly short-circuits on missing settings (AskError).
- **SettingsData depth:** Delegates to `Data.GetChild` which has `MaxNavigationDepth=100`.

## Code example

The medium finding — `SqliteDataSource.cs:256`:

```csharp
// CURRENT: catches JsonException but not InvalidOperationException
private static object? DeserializeValue(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        return Data.UnwrapJsonElement(doc.RootElement); // throws InvalidOperationException at depth > 128
    }
    catch (JsonException)
    {
        return json;
    }
}

// FIX: add InvalidOperationException to catch filter
private static object? DeserializeValue(string json)
{
    try
    {
        using var doc = JsonDocument.Parse(json);
        return Data.UnwrapJsonElement(doc.RootElement);
    }
    catch (JsonException)
    {
        return json;
    }
    catch (InvalidOperationException)
    {
        return json; // depth exceeded — treat as raw string
    }
}
```

## Verdict

**PASS** — no critical or high severity findings. The medium finding (uncaught InvalidOperationException) requires specific conditions (externally-sourced JSON with 128+ nesting stored in settings) and results in unhandled exception, not process crash. Recommend fixing in a follow-up.
