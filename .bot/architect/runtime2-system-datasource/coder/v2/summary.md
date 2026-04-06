# v2 Summary: Fix Code Analyzer Findings

## What this is
Addresses all findings from the code analyzer's v1 review of the DataSource + Settings Bridge feature. Fixes code quality issues and adds missing test coverage for security-critical and integration paths.

## What was done

### Code Fixes (3 files)

**`PLang/App/Engine/DataSource/SqliteDataSource.cs`**
- `DeserializeValue`: bare `catch` → `catch (JsonException)` — no longer masks non-JSON errors
- `EnableWalMode`: bare `catch` → `catch (SqliteException)` — consistency

**`PLang/App/Engine/Memory/Variables.cs`**
- `Clone()` now preserves specialized `Data` subclasses (SettingsData, DynamicData) by reference instead of creating plain `Data` objects. This fixes the type-loss bug where cloning the System actor's Variables would silently break Settings lazy-loading.

**`PLang/App/Engine/Context/Actor.cs`**
- `DataSource` property now uses `Lazy<IDataSource>` instead of `??=` for thread-safe lazy initialization. `DisposeAsync` checks `IsValueCreated` to avoid triggering creation during cleanup.

### Tests Added (14 new tests)

**`PLang.Tests/App/Modules/datasource/DataSourceTests.cs`** (+9 tests)
- SanitizeTableName: special chars stripped, underscores preserved, empty → "default_table", case normalization
- ClassifyException: locked, disk error, corrupt, permission denied, unknown default

**`PLang.Tests/App/Modules/settings/SettingsDataTests.cs`** (+5 tests)
- Nested path navigation: `Settings.Config.SubKey` with JSON object value
- Variables.Clone preserves SettingsData (success + AskError paths)
- Error propagation integration: Variables.Get("Settings.MissingKey") returns AskError (simulates LazyParamsGenerator's `__Resolve<T>` call path)
- Error propagation integration: Variables.Get("Settings.ApiKey") returns success

### Not changed
- `__resolutionError` single-check pattern — design limitation, works correctly for Settings. Out of scope.

## Test results
All 1460 C# tests pass (14 new, 0 failures).

## Code example

**Variables.Clone() fix** — the key pattern change:
```csharp
// Before: always created plain Data, losing virtual GetChild override
clone._variables[kvp.Key] = new Data(kvp.Value.Name, clonedValue, kvp.Value.Type);

// After: preserve specialized subclasses by reference
if (kvp.Value is DynamicData || kvp.Value.GetType() != typeof(Data))
{
    clone._variables[kvp.Key] = kvp.Value;
}
else
{
    var clonedValue = kvp.Value.Value.DeepClone();
    clone._variables[kvp.Key] = new Data(kvp.Value.Name, clonedValue, kvp.Value.Type);
}
```

## What the reviewer flagged and how it was fixed
1. **Bare catch masks exceptions** → Narrowed to specific exception types
2. **Variables.Clone type loss** → Preserve subclasses by reference
3. **Thread-unsafe lazy init** → `Lazy<T>`
4. **Untested SanitizeTableName** → 4 tests including SQL injection attempt
5. **Untested ClassifyException** → 5 tests covering all branches
6. **Untested nested paths** → Test with JSON object value
7. **Untested Clone preservation** → 2 tests (success + error)
8. **Untested error propagation** → 2 integration tests simulating generated code path
