# Code Analyzer v3 Summary — runtime2-setup-goal

## What this is

Deep re-analysis of the entire branch, looking beyond the Setup system into the DataSource/Settings bridge wiring. Found a high-severity behavioral issue that tests mask.

## What was done

Traced the full execution path from `Executor.Run2` through `RunGoalAsync` → `Goal.RunAsync` → `Steps.RunAsync` → LazyParamsGenerator's `__Resolve<T>` to verify which MemoryStack `%Settings.ApiKey%` resolves against.

**Finding 1 (High):** SettingsData is registered only on `System.Context.MemoryStack` (Actor.cs:64-68), but all PLang execution uses `User.Context` (Engine/this.cs:145). `%Settings.ApiKey%` silently resolves to null from PLang code. All 15 SettingsData tests use `_engine.System.Context.MemoryStack`, masking the gap. The settings action handlers (`get`/`set`/`remove`) work because they access `Context.Engine.System.DataSource` directly, bypassing MemoryStack.

**Observation (Low):** MemoryStack.Clone shares SettingsData by reference. Context setter mutates the shared instance. Theoretical until child contexts have real call sites.

## Verdict: FAIL — SettingsData bridge unreachable from PLang code
