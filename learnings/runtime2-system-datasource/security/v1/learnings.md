# Security Learnings — runtime2-system-datasource v1

## 1. Exception type mismatch in catch filters at serialization boundaries

**What:** When `Data.UnwrapJsonElement` hits depth > 128, it throws `InvalidOperationException`, not `JsonException`. Any catch filter that only handles `JsonException` misses the depth guard.

**Why it matters:** The depth guard is a security mitigation (prevents StackOverflow). If the exception it produces isn't caught by the same boundary that catches other JSON errors, the mitigation is partially bypassed — the process doesn't crash, but the exception propagates uncaught instead of being handled gracefully.

**Pattern to watch for:** Whenever `Data.UnwrapJsonElement` is called, trace ALL exception types it can produce. Currently: `InvalidOperationException` (depth), plus whatever `JsonElement` methods throw. The caller must catch both `JsonException` and `InvalidOperationException`.

**Locations:** `SqliteDataSource.DeserializeValue` (this branch), `fromJson.cs` (already delegates to Data.UnwrapJsonElement and catches in the action handler).

## 2. Actor names as hardcoded enum → safe path construction

**What:** `Engine.GetActor()` uses a switch on lowercased name — only "system", "service", "user" are valid. `Actor.CreateDataSource` builds DB path as `.db/{name}.sqlite`.

**Why it matters:** This makes actor-name-based path traversal impossible. Good pattern: constrain names at resolution, not at consumption.

**Pattern:** When validating user-provided names that map to file paths, validate at the resolution point (switch/enum) rather than trying to sanitize at every consumption point.

## 3. SanitizeTableName is a good SQL injection defense pattern

**What:** `SanitizeTableName` strips all non-alphanumeric/underscore chars, lowercases, falls back to "default_table" for empty. Combined with bracket quoting and parameterized keys.

**Why it matters:** Defense in depth — even if bracket quoting is bypassed, the sanitization removes all dangerous characters. The test suite includes an explicit SQL injection test (`"settings; DROP TABLE settings"`).

**Pattern:** For identifiers that can't be parameterized (table/column names), use allowlist character filtering + bracket/backtick quoting + parameterized values for data.

## 4. Sync-over-async is safe for SQLite but constrains interface evolution

**What:** `SettingsData.GetChild` uses `.GetAwaiter().GetResult()` because SQLite has no real async I/O. The `IDataSource` interface returns `Task<Data>` for forward compatibility.

**Why it matters:** If `IDataSource` is ever implemented with a real async backend (PostgreSQL, Redis), the sync-over-async in SettingsData would deadlock on SynchronizationContext-bound threads. Document this constraint on the interface.

**Pattern:** When using sync-over-async, add a comment on the INTERFACE (not just the caller) documenting that implementations must be synchronous or the caller must be updated.

## 5. TOCTOU in DDL migrations

**What:** SqliteSettingsRepository checks "does table X exist?" then renames it. Two concurrent processes can both see the old table, one rename succeeds, the other fails.

**Pattern:** For schema migrations, use `ALTER TABLE IF EXISTS` (not available in SQLite) or wrap in a transaction with a lock, or catch the expected error on the second attempt.
