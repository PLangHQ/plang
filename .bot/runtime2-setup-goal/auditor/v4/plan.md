# Auditor v4 Plan — Setup.goal Run-Once Execution System

## What I'm reviewing

Coder v4 / Tester v4 PASS. This is the first auditor review of the `runtime2-setup-goal` branch. The branch implements:

1. **Setup.goal run-once system** — Setup goals run once-per-step at startup, tracked in system.sqlite
2. **SettingsData sharing** — `%Settings.X%` resolves from all actor contexts (not just System)
3. **Steps.RunAsync refactor** — Step iteration moved from Goal.Methods.cs to Steps.@this (OBP rule 5)

## Files to review

**New code:**
- `PLang/App/Goals/Setup/this.cs` — Setup object
- `PLang.Tests/App/Goals/Setup/SetupTests.cs` — 18 setup tests

**Modified code:**
- `PLang/App/Goals/Goal/Steps/this.cs` — Steps.RunAsync with run-once check
- `PLang/App/Goals/Goal/Methods.cs` — Delegation to Steps.RunAsync
- `PLang/App/Goals/this.cs` — EngineGoals.Setup property, AllIncludingSetup, filtering
- `PLang/App/Context/PLangContext.cs` — Setup property, Clone
- `PLang/App/Context/Actor.cs` — SettingsData shared registration
- `PLang/App/this.cs` — SettingsVariable
- `PLang/Executor.cs` — Run2 setup call
- `PLang.Tests/App/Modules/settings/SettingsDataTests.cs` — Updated to User context

## Review checklist

1. OBP compliance (5 rules)
2. Contract consistency (GetAsync vs Get setup filtering)
3. Error handling (behavior methods never throw)
4. Test quality (assertions catch real bugs?)
5. Ripple impact (foundation changes affect everything above)
6. Setup discovery gap (goals not loaded before Setup.RunAsync)
