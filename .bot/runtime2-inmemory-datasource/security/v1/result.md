# Security Audit Results — runtime2-inmemory-datasource v1

## Attack Surface Map

### 1. In-Memory SQLite (SqliteDataSource.InMemory)

**What's new:** Static factory `InMemory(name)` creates a SQLite shared-cache in-memory DB kept alive by a sentinel `SqliteConnection`.

**Trust boundary:** Process-level. Shared-cache means same-name DBs in the same process share state. Currently safe: test runner and builder both run sequentially and dispose between uses.

**Mitigations verified:**
- `SanitizeTableName` strips all non-alphanumeric/underscore → SQL injection via table names impossible
- Keys use `@key` parameterized queries → SQL injection via keys impossible
- Values go through `JsonSerializer.Serialize` → no raw SQL from values
- `EnsureTable` uses `CREATE TABLE IF NOT EXISTS` with sanitized names → idempotent, safe

**Gap:** No `_disposed` check on CRUD methods. After `Dispose()`, operations throw `SqliteException` instead of `ObjectDisposedException`. Not exploitable — the exception is caught by the method's own `catch (SqliteException)` handler and returned as `Data.FromError`.

### 2. DeserializeValue Exception Chain (Carry-Forward)

**The chain:**
```
SqliteDataSource.Get()
  → DeserializeValue(json)
    → JsonDocument.Parse(json)          // MaxDepth=64, throws JsonException (CAUGHT)
    → Data.UnwrapJsonElement(element)    // MaxDepth=128, throws InvalidOperationException (NOT CAUGHT)
```

**Key insight:** `JsonDocument.Parse` with default `MaxDepth=64` rejects JSON deeper than 64 levels as `JsonException`, which IS caught. The `Data.UnwrapJsonElement` guard at 128 is unreachable from this code path because the parser fails first.

**Why still medium:** Violates the "behavior methods never throw" contract. If a future change raises `JsonDocumentOptions.MaxDepth` (or if `UnwrapJsonElement` is called from another path), the `InvalidOperationException` escapes the catch blocks in `Get()` and `GetAll()`. Defensive fix is cheap.

**Proposed fix:**
```csharp
// In DeserializeValue, add:
catch (InvalidOperationException)
{
    return json; // Same fallback as JsonException
}
```

### 3. SettingsData.GetChild

**Verified secure:**
- Depth parameter propagated: `child.GetChild(remaining, depth + 1)` → inherits `Data.GetChild`'s `MaxNavigationDepth=100` guard
- No injection: key is extracted from the dot-notation path, passed as parameterized `@key` to DataSource.Get
- Error handling: returns `AskError` for missing keys, `Data.FromError` for DB errors

**Design constraint:** `.GetAwaiter().GetResult()` is sync-over-async. Safe for SQLite (no async I/O under the hood). Would deadlock with a truly async `IDataSource` on a `SynchronizationContext`-bound thread. Documented in code, accepted risk.

### 4. Building.IsEnabled

**Same pattern as Testing.IsEnabled:** Public `{ get; set; }`. Under PLang's user-sovereign threat model, the user owns their .pr files and can set any engine property — this is by design, not a vulnerability.

**Side effect analysis:** `Actor.CreateDataSource()` is lazy via `Lazy<IDataSource>`. Setting `Building.IsEnabled = true` AFTER `DataSource` was already accessed has no effect — the file-backed DB was already created. Setting it BEFORE first access switches to in-memory. This is correct behavior.

### 5. SQL Injection — Full Verification

| Input | Protection | Result |
|-------|-----------|--------|
| Table name | `SanitizeTableName` strips non-alnum/underscore + bracket-quotes | Safe |
| Key | `@key` parameter | Safe |
| Value | `JsonSerializer.Serialize` | Safe |
| EnsureTable DDL | Same sanitized name | Safe |

Tested mental model: `SanitizeTableName("'; DROP TABLE --")` → `DROPTABLE` → `[droptable]`. Injection impossible.

## Findings Summary

| ID | Severity | Category | Status | Description |
|----|----------|----------|--------|-------------|
| 1 | Medium | Deserialization | Open | DeserializeValue: InvalidOperationException not caught (currently unreachable) |
| 2 | Low | Resource | Open | Use-after-dispose: no _disposed check on CRUD |
| 3 | Low | Resource | Accepted | In-memory DB name collision with concurrent engines |
| 4 | Low | Design | Accepted | sync-over-async in SettingsData.GetChild |

## Verdict: PASS

No critical or high severity findings. The in-memory datasource adds no new attack surface — it's a construction path variation of the existing SqliteDataSource with proper isolation for its intended use cases (testing, building).
