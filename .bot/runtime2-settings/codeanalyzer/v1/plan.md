# Code Analyzer v1 — Plan

## Scope
Analyze all changed files in `runtime2-settings` branch (vs `runtime2` merge base). Three-pass: OBP compliance, simplification, readability.

## Files to analyze (new production code)
1. `PLang/App/Settings/ISettings.cs` — marker interface
2. `PLang/App/Settings/this.cs` — settings registry
3. `PLang/App/Settings/Scope.cs` — key-value scope
4. `PLang/App/Settings/ModuleView.cs` — context-bound view
5. `PLang/App/actions/archive/Settings.cs` — first ISettings impl
6. `PLang/App/actions/archive/types.cs` — result types

## Files to analyze (diffs only)
7. `PLang/App/this.cs` — Settings property added
8. `PLang/App/Context/PLangContext.cs` — SettingsScope added
9. `PLang/App/Goals/Goal/Methods.cs` — scope save/restore
10. `PLang/App/GlobalUsings.cs` — new aliases

## Test files (lighter pass)
11. `PLang.Tests/App/Settings/SettingsTests.cs`
12. `PLang.Tests/App/Settings/ScopeTests.cs`
13. `PLang.Tests/App/Settings/ModuleViewTests.cs`
14. `Tests/App/Settings/SetMaxGzipSize/Start.test.goal`

## Output
- `v1/result.md` — full per-file analysis
- `v1/summary.md` — session summary
- Root `summary.md` — cross-session summary
