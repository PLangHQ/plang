# Code Analyzer v1 Summary — runtime2-inmemory-datasource

## What this is
Code analysis of the in-memory SQLite datasource feature and related bugfixes. The branch adds `SqliteDataSource.InMemory()` with a sentinel connection pattern, a `Building` mode object on Engine, and routes Actor's DataSource creation through the mode flags. Several bugfixes tag along: Variables array index navigation, AfterStep firing on failure, list.unique return type, and Settings→SettingsV1 migration.

## What was done
5-pass analysis (OBP, simplification, readability, behavioral reasoning, deletion test) across 10 changed production files. Read all test files and architect spec for context.

### Findings

**Finding #1 (Medium): Step/Methods.cs — AfterStep behavioral contract change**
- The removal of `if (!result.Success) return result;` means AfterStep events now fire on failure, not just success
- This was done so the test runner can track assertion failures via AfterStep
- Correct design choice, but undocumented contract change — existing AfterStep handlers may assume success-only
- Files: `PLang/App/Goals/Goal/Steps/Step/Methods.cs:74-75`
- Recommendation: Document in `good_to_know.md`

**Finding #2 (Low): SqliteSettingsRepository.cs — TOCTOU in migration**
- Check-then-rename for Settings→SettingsV1 is not atomic
- Two concurrent processes could race, second rename fails with exception
- Files: `PLang/Services/SettingsService/SqliteSettingsRepository.cs:163-167`
- Recommendation: Wrap in try/catch or remove the rename since CREATE TABLE IF NOT EXISTS handles it

### Files analyzed
- `PLang/App/DataSource/SqliteDataSource.cs` — CLEAN
- `PLang/App/Build/this.cs` — CLEAN
- `PLang/App/Context/Actor.cs` — CLEAN
- `PLang/App/this.cs` — CLEAN
- `PLang/App/GlobalUsings.cs` — CLEAN
- `PLang/App/Memory/Variables.cs` — CLEAN
- `PLang/App/Goals/Goal/Steps/Step/Methods.cs` — NEEDS WORK (undocumented behavior change)
- `PLang/App/Test/this.cs` — CLEAN
- `PLang/App/actions/list/unique.cs` — CLEAN
- `PLang/Services/SettingsService/SqliteSettingsRepository.cs` — NEEDS WORK (minor TOCTOU)

## Code example

The core pattern — sentinel keeps in-memory DB alive:

```csharp
private SqliteDataSource(string name, bool inMemory)
{
    _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = name,
        Mode = SqliteOpenMode.Memory,
        Cache = SqliteCacheMode.Shared
    }.ToString();

    _sentinel = new SqliteConnection(_connectionString);
    _sentinel.Open();
}
```

Actor navigates Engine to decide:

```csharp
private IDataSource CreateDataSource()
{
    if (Engine.Testing.IsEnabled || Engine.Building.IsEnabled)
        return SqliteDataSource.InMemory(Name.ToLowerInvariant());
    // ... file-backed path
}
```

## Verdict: PASS
