# Security Audit — runtime2-settings Branch

## Scope

This branch adds a Settings infrastructure to App: ~200 LOC of new production code across 6 files, plus 377 LOC of tests. The feature provides strongly-typed, goal-scoped module configuration with a resolution chain: context scope -> parent scope -> engine defaults -> class defaults.

## New Files
- `Engine/Settings/this.cs` (87 LOC) — Settings registry, Resolve<T>, Set, Cast<T>, For<T>
- `Engine/Settings/Scope.cs` (30 LOC) — ConcurrentDictionary wrapper, key-value store
- `Engine/Settings/ModuleView.cs` (37 LOC) — Context-bound view with module prefix
- `Engine/Settings/ISettings.cs` (18 LOC) — Marker interface
- `actions/archive/Settings.cs` (28 LOC) — First use case (gzip max size, compression level)
- `actions/archive/types.cs` (14 LOC) — settingsResult record

## Modified Files
- `Engine/Context/PLangContext.cs` — Added `SettingsScope` property, preserved in Clone()
- `Engine/Goals/Goal/Methods.cs` — Save/null/restore SettingsScope in RunAsync
- `Engine/this.cs` — Added `Settings` property
- `GlobalUsings.cs` — Added EngineSettings and SettingsScope aliases

## Approach

1. Read all new/changed code (done)
2. Analyze against PLang threat model (user-sovereign, signatures are the wall)
3. Blue team: map attack surface, trust boundaries, mitigations
4. Red team: attempt exploitation of identified vectors
5. Write security-report.json and summary

## Key Questions
- Does the Settings system introduce new trust boundary crossings?
- Can settings be used to weaken existing security controls (e.g., lowering MaxDecompressedSize)?
- Are there resource exhaustion vectors via unbounded scope dictionaries?
- Does the scope chain traversal have depth issues?
