# Tester Summary — runtime2-system-datasource

**v2**: FAIL — Test runner can't detect assertion failures (AfterStep skipped on error), SetMaxGzipSize is false green (maps to variable module), no PLang settings tests, SettingsData error path uncovered. See [v2/summary.md](v2/summary.md).

**v3**: FAIL — Coder fixed test runner (AfterStep + __stepResult) and added SettingsCrud PLang test + error path test. BUT: SetMaxGzipSize still shows PASS despite static analysis proving it should fail (test runner fix incomplete — needs runtime debugging). SettingsCrud blocked by v1/v2 schema collision. Test runner has 2.8% C# coverage. See [v3/summary.md](v3/summary.md).
