# Tester v3 Plan — DataSource + Settings Bridge

## Context
- Reviewing coder's v2 fixes that addressed tester v2's findings (F1-F4)
- v2 verdict was FAIL due to: test runner bug (F1), false green PLang test (F2), no PLang settings tests (F3), uncovered error path (F4)
- Coder fixed: test runner AfterStep/`__stepResult` (c4dbbb30), added SettingsCrud PLang test (e80c0dff), added SettingsData error path test (e80c0dff)

## What I'll Do
1. Run C# tests → 1461 pass ✓
2. Run PLang tests → 22 pass, 1 fail (SettingsCrud schema collision)
3. Analyze coder's fixes for each v2 finding
4. Run coverage analysis
5. Hunt for remaining false greens and gaps
6. Write test-report.json, verdict.json, summary.md

## Key Findings So Far
- SetMaxGzipSize still shows PASS despite being a known false green — test runner fix may not work for this case
- SettingsCrud fails with v1/v2 schema collision ("table settings has no column named data")
- C# test coverage: SettingsData.cs 100%, Actor.cs 100%, SqliteDataSource.cs ~70% (error paths uncovered)
- Test runner itself has 2.8% coverage (4/143 lines)
