# Code Analysis Plan — v1

## Scope

Review all code changes on branch `architect/runtime2-system-datasource` against OBP compliance, simplification, readability, behavioral correctness, and test coverage.

## Files to Analyze

**New files (10):**
- `PLang/App/Engine/DataSource/IDataSource.cs`
- `PLang/App/Engine/DataSource/SqliteDataSource.cs`
- `PLang/App/Engine/DataSource/SettingsData.cs`
- `PLang/App/Engine/Errors/AskError.cs`
- `PLang/App/Engine/Errors/DataSourceError.cs`
- `PLang/App/actions/settings/get.cs`
- `PLang/App/actions/settings/set.cs`
- `PLang/App/actions/settings/remove.cs`
- `PLang/App/actions/settings/types.cs`
- Test files (2)

**Modified files (3):**
- `PLang/App/Engine/Memory/Data.Navigation.cs` — GetChild made virtual
- `PLang/App/Engine/Context/Actor.cs` — DataSource + SettingsData registration
- `PLang.Generators/LazyParamsGenerator.cs` — error propagation

## Analysis Method

5-pass analysis: OBP Compliance → Simplification → Readability → Behavioral Reasoning → Deletion Test
