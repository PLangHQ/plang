# Tester v3 Summary — DataSource + Settings Bridge

## What this is
Test quality review of coder v2's fixes for tester v2 findings (F1-F4). The coder fixed the test runner (AfterStep fires on failure, `__stepResult` stored in MemoryStack), added a SettingsCrud PLang test with correct settings.set/get/remove mapping, and added a SettingsData error path test.

## Test Run Results
- **C# tests**: 1461 passed, 0 failed
- **PLang tests**: 22 passed, 1 failed (SettingsCrud: v1/v2 schema collision)
- **Coverage**: TUnit `--coverage` (cobertura), SettingsData.cs at 100%

## Findings by v2 Finding

### F1 (Test runner can't detect assertion failures) — PARTIALLY FIXED
The coder's fix is structurally correct:
- `__stepResult` is stored in MemoryStack BEFORE AfterStep fires (Step/Methods.cs line 75)
- AfterStep now fires even on step failure (moved below error handling)
- Goal-level fallback catches AssertionError when Failures.Count == 0 (Test/this.cs lines 148-158)

**BUT**: SetMaxGzipSize still shows PASS despite being a known false green. Full static trace of the execution path shows the assert.equals step SHOULD return AssertionError:
1. Step 0: `variable.set` sets "max gzip size" = 20971520
2. Step 1: `variable.get("archive.max")` → not found → Data.Ok(null) → return mapping skipped (result.Value is null) → `maxSize` never set
3. Step 2: `assert.equals(20971520, %maxSize%)` → `__Resolve` gets null from MemoryStack → `ConvertTo(null, typeof(object))` → null → `AreEqual(20971520, null)` → false → AssertionError

Yet the test runner reports PASS. Possible causes:
- Lifecycle caching in `_eventContainers.GetOrAdd(step, ...)` might prevent AfterStep handler from reaching step execution
- The goal-level fallback (line 146-158) might not fire for some reason
- A runtime behavior not visible in static analysis

**Action needed**: Runtime debugging with console output in TrackAssertionFailures and the fallback path.

### F2 (Settings PLang test was false green) — FIXED
The coder created `Tests/Runtime2/Settings/SettingsCrud/Start.test.goal` with correct PLang syntax. The .pr file correctly maps to `settings.set`, `settings.get`, `assert.equals`, and `settings.remove` (verified). However, the test cannot run due to F2b below.

### F2b (NEW: SettingsCrud blocked by v1/v2 schema collision)
`SettingsCrud/Start.test.goal` fails with "SQLite Error 1: 'table settings has no column named data'". The v1 `SqliteSettingsRepository` creates a "Settings" table with columns (key, value). The v2 `SqliteDataSource` creates "settings" with columns (key, data). SQLite table names are case-insensitive → collision. The coder noted this as a known blocker in the commit message but didn't resolve it.

### F3 (No PLang tests for settings module) — BLOCKED
Addressed by SettingsCrud test, but blocked by F2b. Zero pipeline coverage for settings.set/get/remove.

### F4 (SettingsData error path untested) — FIXED ✓
New test `SettingsData_GetChild_CorruptDatabase_ReturnsDataSourceError` covers lines 54-55 by corrupting the SQLite DB file. Asserts `child.Error is DataSourceError`. Coverage confirms SettingsData.cs is at 100%.

## New Findings

### F8: Test runner has 2.8% coverage (MAJOR)
The test runner (`PLang/Runtime2/Engine/Test/this.cs`) — the gatekeeper for all PLang tests — has only 4/143 lines covered by C# tests. TrackAssertionFailures, RunSingleTest, and PrintSummary are untested. Test runner bugs can only be detected by manual observation.

## Coverage Highlights
| File | Coverage | Notes |
|------|----------|-------|
| SettingsData.cs | 100% | F4 fix confirmed |
| Actor.cs | 100% | Lazy<IDataSource> fully tested |
| MemoryStack.cs | 98.7% | Clone preserves subclasses |
| SqliteDataSource.cs | 69.6% | Error catch blocks untested |
| Test/this.cs | 2.8% | Test runner essentially untested |
| settings handlers | 0% | Generated code, needs pipeline test |

## Verdict: FAIL (needs-fixes)

Three issues must be resolved:
1. **SetMaxGzipSize must be deleted or debugged** — it's a false green that the test runner doesn't catch
2. **SettingsCrud schema collision must be fixed** — the only settings pipeline test is blocked
3. **Test runner needs at least basic C# tests** — 2.8% coverage for a critical component is unacceptable
