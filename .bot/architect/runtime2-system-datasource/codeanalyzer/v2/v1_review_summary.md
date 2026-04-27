# v1 Review Summary

## What v1 Found

The v1 code analysis (verdict: FAIL) identified 8 findings across 5 severity levels:

### High (2)
1. **LazyParamsGenerator error propagation untested** — The full path `%Settings.Key%` → generated code → `Variables.Get()` → `SettingsData.GetChild()` → `AskError` → `__resolutionError` had zero test coverage.
2. **`SanitizeTableName` untested** — SQL injection defense with no adversarial-input tests.

### Medium-High (1)
3. **Variables.Clone() loses SettingsData type** — `Clone()` created `new Data(name, value, type)`, silently losing the virtual `GetChild` override.

### Medium (2)
4. **Bare `catch` in `DeserializeValue`** — Caught all exceptions (including `InvalidOperationException` from depth bombs) instead of just `JsonException`.
5. **`__resolutionError` single-check pattern** — Errors from optional property resolution during `Run()` silently swallowed.

### Low (3)
6. **Actor.DataSource lazy init not thread-safe** — `??=` can create duplicate instances.
7. **Nested settings path untested** — `Settings.Config.SubKey` had no coverage.
8. **ClassifyException untested** — Error classification with no test coverage.

## What the Coder Fixed (v2)

The coder addressed all findings except #5 (acknowledged as a LazyParamsGenerator design limitation, not a bug in this feature):

- **SqliteDataSource.cs**: `catch` → `catch (JsonException)` in `DeserializeValue`, `catch` → `catch (SqliteException)` in `EnableWalMode`
- **Variables.cs**: `Clone()` now preserves Data subclasses by reference via `GetType() != typeof(Data)` check
- **Actor.cs**: `??=` → `Lazy<IDataSource>`, `DisposeAsync` checks `IsValueCreated`
- **14 new tests**: SanitizeTableName (4), ClassifyException (5), nested paths (1), Clone preservation (2), error propagation integration (2)
