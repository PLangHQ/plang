# Auditor v1 Plan — runtime2-system-datasource

## Scope
Review the DataSource + Settings Bridge feature as implemented by the coder, validated by the tester (v3: PASS, 1465 C# + 23/23 PLang), and audited by the security agent (v1: PASS).

## What Changed
- **New**: `IDataSource`, `SqliteDataSource`, `SettingsData`, `DataSourceError`, `AskError`, `Actor.DataSource`
- **New handlers**: `settings/get`, `settings/set`, `settings/remove`, `settings/types`
- **Modified**: `MemoryStack.cs` (array navigation fix + Clone for SettingsData), `Data.Navigation.cs` (minor), `LazyParamsGenerator.cs` (resolution error propagation), `Step/Methods.cs` (stepResult storage), `list/unique.cs` (return type fix), `SqliteSettingsRepository.cs` (Settings→SettingsV1 rename)
- **Tests**: `DataSourceTests.cs` (22 tests), `SettingsDataTests.cs` (18 tests), `MemoryStackTests.cs` (+4 tests), `ListTests.cs` (updated)

## Review Checklist
1. **OBP compliance** — 5 rules checked against all new code
2. **Contract integrity** — IDataSource promise vs SqliteDataSource behavior, SettingsData.GetChild behavior
3. **Error handling completeness** — every try/catch covers all thrown exception types
4. **LazyParamsGenerator** — resolution error propagation correctness
5. **Test adequacy** — new code path checklist for each branch
6. **Cross-cutting** — MemoryStack.Clone correctness, thread safety, lifecycle management

## Deliverables
- `auditor-report.json` at `.bot/runtime2-system-datasource/`
- `verdict.json` at `.bot/runtime2-system-datasource/auditor/v1/`
- `summary.md` at `.bot/runtime2-system-datasource/auditor/v1/`
- Updated `summary.md` at `.bot/runtime2-system-datasource/auditor/`
- Session entry in `report.json`
