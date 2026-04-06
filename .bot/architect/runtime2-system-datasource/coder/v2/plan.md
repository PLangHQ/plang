# Plan: Fix Code Analyzer Findings (v2)

## Objective
Address all findings from the code analyzer's v1 review. Fix code quality issues and add missing test coverage.

## Changes

### 1. Fix bare `catch` in `SqliteDataSource.DeserializeValue` (Medium)
**File:** `PLang/App/Engine/DataSource/SqliteDataSource.cs`
- Change `catch` → `catch (JsonException)` in `DeserializeValue()` (line 266)
- Also change bare `catch` → `catch (SqliteException)` in `EnableWalMode()` (line 51) for consistency

### 2. Fix Variables.Clone() to preserve SettingsData type (Medium-High)
**File:** `PLang/App/Engine/Memory/Variables.cs`
- In `Clone()`, detect `SettingsData` (and `DynamicData`) instances and preserve them by reference instead of creating plain `Data`.
- SettingsData is stateless (it loads from DB each time) so sharing by reference is safe.
- DynamicData is already a factory-style type, so same treatment.

### 3. Fix Actor.DataSource thread safety (Low)
**File:** `PLang/App/Engine/Context/Actor.cs`
- Replace `??=` with `Lazy<IDataSource>` for thread-safe lazy initialization.

### 4. Add SanitizeTableName tests (High)
**File:** `PLang.Tests/App/Modules/datasource/DataSourceTests.cs`
- Test that special characters are stripped from table names
- Test that empty input falls back to "default_table"
- Test SQL injection attempt (e.g., `"settings; DROP TABLE settings"`)

### 5. Add ClassifyException tests (Medium)
**File:** `PLang.Tests/App/Modules/datasource/DataSourceTests.cs`
- Test each classification branch: locked, disk error, corrupt, permission, default

### 6. Add nested settings path test (Low)
**File:** `PLang.Tests/App/Modules/settings/SettingsDataTests.cs`
- Test `Settings.Config.SubKey` pattern with JSON object value

### 7. Add Variables.Clone() preserves SettingsData test (Medium-High)
**File:** `PLang.Tests/App/Modules/settings/SettingsDataTests.cs`
- Clone the System actor's Variables, verify Settings still works in the clone

### 8. Add LazyParamsGenerator error propagation integration test (High)
**File:** `PLang.Tests/App/Modules/settings/SettingsDataTests.cs`
- Simulate the generated code path: create parameters with `%Settings.MissingKey%`, resolve through Variables, verify error propagation.
- Note: We can't easily test the actual source-generated code in a unit test, but we CAN test the Variables.Get() → SettingsData.GetChild() → AskError chain that the generated code calls.

### 9. Update report.json
Add a new session entry for v2.

## What I'm NOT changing
- **`__resolutionError` single-check pattern** (finding #5) — This is a design limitation of the LazyParamsGenerator, not a bug in this branch. The pattern works correctly for the Settings use case. Fixing it properly requires rethinking the generated code flow, which is out of scope for this fix round.

## Order of operations
1. Code fixes (items 1-3)
2. Tests (items 4-8)
3. Run all tests to verify
4. Session reporting and commit
