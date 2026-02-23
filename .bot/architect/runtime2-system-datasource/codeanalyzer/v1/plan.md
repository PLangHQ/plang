# Code Analysis Plan — v1

## Scope

Review all code changes on branch `architect/runtime2-system-datasource` against OBP compliance, simplification, readability, behavioral correctness, and test coverage.

## Files to Analyze

**New files (10):**
- `PLang/Runtime2/Engine/DataSource/IDataSource.cs`
- `PLang/Runtime2/Engine/DataSource/SqliteDataSource.cs`
- `PLang/Runtime2/Engine/DataSource/SettingsData.cs`
- `PLang/Runtime2/Engine/Errors/AskError.cs`
- `PLang/Runtime2/Engine/Errors/DataSourceError.cs`
- `PLang/Runtime2/actions/settings/get.cs`
- `PLang/Runtime2/actions/settings/set.cs`
- `PLang/Runtime2/actions/settings/remove.cs`
- `PLang/Runtime2/actions/settings/types.cs`
- Test files (2)

**Modified files (3):**
- `PLang/Runtime2/Engine/Memory/Data.Navigation.cs` — GetChild made virtual
- `PLang/Runtime2/Engine/Context/Actor.cs` — DataSource + SettingsData registration
- `PLang.Generators/LazyParamsGenerator.cs` — error propagation

## Analysis Method

5-pass analysis: OBP Compliance → Simplification → Readability → Behavioral Reasoning → Deletion Test
