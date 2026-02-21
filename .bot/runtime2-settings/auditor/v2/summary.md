# Auditor v2 — runtime2-settings Review

## What this is

Code review of the Settings infrastructure — strongly-typed, goal-scoped module configuration for Runtime2. ~200 LOC production code, 23 dedicated tests, 1262 total tests pass.

## What was reviewed

- `Engine/Settings/this.cs` — Resolve<T>, Set, Cast<T>, For<T>
- `Engine/Settings/Scope.cs` — ConcurrentDictionary wrapper
- `Engine/Settings/ModuleView.cs` — context-bound view
- `Engine/Settings/ISettings.cs` — marker interface
- `Engine/Context/PLangContext.cs` — SettingsScope property, Clone
- `Engine/Goals/Goal/Methods.cs` — save/null/restore pattern
- `Engine/this.cs` — Settings property on Engine
- `actions/archive/Settings.cs` — first use case
- `actions/archive/types.cs` — result types
- All 23 Settings tests

## Findings

### 1 Major

1. **Clone() shares Scope by reference** — `PLangContext.Clone()` copies `SettingsScope = SettingsScope`, sharing the same `Scope` instance. Writes in the clone pollute the original. Test only verifies reading, not write isolation.

### 2 Minor

2. **Save/restore pattern complexity growing** — Three save/restore pairs (Goal, Step, SettingsScope) in RunAsync. Consider extracting to disposable scope.
3. **GoalRunAsync test is simulation** — Doesn't exercise actual Methods.cs code path. Acknowledged as deferred by tester.

### 1 Nit

4. **Bare catch in Cast<T>** — Swallows all exceptions including critical ones. Accepted-risk by tester and security bot.

## OBP Assessment

Clean. Navigate through `engine.Settings`, behavior on owner. ModuleView is a lightweight context-stamped accessor. Scope owns its dictionary. No OBP violations.

## Verdict

**Approved with one fix recommended.** Finding #1 (Clone shares Scope) should be addressed before merge — it breaks the isolation contract that Clone() implies.
