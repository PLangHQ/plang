# Auditor v4 Summary — Setup.goal Run-Once Execution System

## What this is

First auditor review of the `runtime2-setup-goal` branch. The branch adds three features: (1) a run-once setup system where setup goals execute once-per-step at startup tracked in system.sqlite, (2) SettingsData sharing across all actors so `%Settings.X%` resolves from any context, (3) Steps.RunAsync refactor moving step iteration from Goal.Methods.cs to Steps.@this (OBP rule 5).

## What was done

Reviewed all code changes (diff against runtime2), read full source files for context, ran all 1485 C# tests (pass), traced execution paths through Executor.Run2 → Setup.RunAsync → Goal.RunAsync → Steps.RunAsync.

### OBP compliance: GOOD
- Setup.@this correctly owns its behavior (RunAsync, IsExecuted, Record, IsTolerableError)
- Steps.RunAsync owns step iteration (OBP rule 5 — smart collections)
- Goal.RunAsync delegates to Steps.RunAsync, doesn't iterate directly
- Navigate-don't-pass: Setup.Record takes (step, engine) — step is the object, engine is for navigation

### Code quality: GOOD
- Error handling follows convention: Data return on all paths, no throws
- Record() returns Data for failure detection, checked by caller
- IsTolerableError matches runtime1 patterns ("already exists", "duplicate column name")
- context.Setup set in try/finally — always cleaned up

### Findings: 2 major, 1 minor, 1 nit

**F1 (Major): GetAsync doesn't filter IsSetup.** `Get()` correctly filters `!goal.IsSetup`, but `GetAsync()` and `GetByPrPathAsync()` return goals loaded from disk without checking the flag. A setup goal can be called as a regular goal if it's not in cache when requested.

**F2 (Major): Setup goals never loaded before Setup.RunAsync.** `Executor.Run2` creates a fresh Engine, calls `Setup.RunAsync` immediately. No goals have been loaded into the collection. `Setup.Goals` iterates an empty collection. Setup silently succeeds without running anything. Tests mask this because they manually `Add()` goals.

**F3 (Minor): 'setup' goal name reserved.** `Run2` intercepts `goalName == "setup"` and returns without running any user goal. If a user has a non-setup goal named "Setup", it's silently skipped.

**F4 (Nit): Metadata numeric boxing.** `Record()` stores `stepIndex` as int in a `Dictionary<string, object?>`. If ever deserialized, JSON numeric boxing could bite. Not an issue now — flagged for awareness.

## Verdict: FAIL

F1 and F2 are both major. F1 is a contract gap that could allow setup goals to run outside setup context. F2 means the feature doesn't work in production — the infrastructure is correct but the wiring is incomplete.

## Files reviewed
- `PLang/App/Goals/Setup/this.cs`
- `PLang/App/Goals/Goal/Steps/this.cs`
- `PLang/App/Goals/Goal/Methods.cs`
- `PLang/App/Goals/this.cs`
- `PLang/App/Context/PLangContext.cs`
- `PLang/App/Context/Actor.cs`
- `PLang/App/this.cs`
- `PLang/Executor.cs`
- `PLang.Tests/App/Goals/Setup/SetupTests.cs`
- `PLang.Tests/App/Modules/settings/SettingsDataTests.cs`
- `PLang.Tests/App/Core/GoalsTests.cs`
