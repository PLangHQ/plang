# Code Analyzer v1 — Plan

## Scope
Analyze all changed files in `runtime2-settings` branch (vs `runtime2` merge base). Three-pass: OBP compliance, simplification, readability.

## Files to analyze (new production code)
1. `PLang/Runtime2/Engine/Settings/ISettings.cs` — marker interface
2. `PLang/Runtime2/Engine/Settings/this.cs` — settings registry
3. `PLang/Runtime2/Engine/Settings/Scope.cs` — key-value scope
4. `PLang/Runtime2/Engine/Settings/ModuleView.cs` — context-bound view
5. `PLang/Runtime2/actions/archive/Settings.cs` — first ISettings impl
6. `PLang/Runtime2/actions/archive/types.cs` — result types

## Files to analyze (diffs only)
7. `PLang/Runtime2/Engine/this.cs` — Settings property added
8. `PLang/Runtime2/Engine/Context/PLangContext.cs` — SettingsScope added
9. `PLang/Runtime2/Engine/Goals/Goal/Methods.cs` — scope save/restore
10. `PLang/Runtime2/GlobalUsings.cs` — new aliases

## Test files (lighter pass)
11. `PLang.Tests/Runtime2/Engine/Settings/SettingsTests.cs`
12. `PLang.Tests/Runtime2/Engine/Settings/ScopeTests.cs`
13. `PLang.Tests/Runtime2/Engine/Settings/ModuleViewTests.cs`
14. `Tests/Runtime2/Settings/SetMaxGzipSize/Start.test.goal`

## Output
- `v1/result.md` — full per-file analysis
- `v1/summary.md` — session summary
- Root `summary.md` — cross-session summary
