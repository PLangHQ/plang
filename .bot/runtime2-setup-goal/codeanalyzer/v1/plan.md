# Code Analyzer v1 Plan — runtime2-setup-goal

## Scope

Analyze all new and modified code on this branch. The branch adds two features:
1. **Setup.goal run-once execution system** — Setup goals run once per step hash at app startup
2. **DataSource + Settings bridge** (carried from parent branch) — SQLite key-value persistence and settings variable bridge

## Files to Analyze

### New (Setup system)
- `PLang/App/Engine/Goals/Setup/this.cs` — Setup coordinator
- `PLang.Tests/App/Goals/Setup/SetupTests.cs` — 9 tests

### Modified (Setup system)
- `PLang/App/Engine/Goals/Goal/Steps/this.cs` — Steps.RunAsync with run-once check
- `PLang/App/Engine/Goals/Goal/Methods.cs` — Goal.RunAsync delegates to Steps
- `PLang/App/Engine/Goals/this.cs` — Setup property, setup exclusion from Get
- `PLang/App/Engine/Context/PLangContext.cs` — Setup property, Clone
- `PLang/Executor.cs` — Run2 wiring

### New (DataSource + Settings — from parent branch, included for completeness)
- `PLang/App/Engine/Context/Actor.cs`
- `PLang/App/Engine/DataSource/IDataSource.cs`
- `PLang/App/Engine/DataSource/SqliteDataSource.cs`
- `PLang/App/Engine/DataSource/SettingsData.cs`
- `PLang/App/Engine/Errors/AskError.cs`
- `PLang/App/Engine/Errors/DataSourceError.cs`
- `PLang/App/actions/settings/get.cs, set.cs, remove.cs, types.cs`

## Analysis Method

5-pass analysis:
1. OBP compliance
2. Simplification
3. Readability
4. Behavioral reasoning (focus: setup record-on-failure semantics, error swallowing)
5. Deletion test (what code lacks test coverage)
