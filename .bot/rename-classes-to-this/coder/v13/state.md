# Session v13 State

## Status: Complete
## Task: Remove Core/ folder, promote files to App/ with namespace App

### What was done
1. Created branch `restructure-core-folders` from `runtime2`
2. Moved all 26 files from Core/ into App/ subfolders (Goals/, Steps/, Actions/, Events/, Cache/, Execution/) and root
3. Removed Core/ folder entirely
4. Changed namespace from `App.Core` to `App` in all 26 files
5. Updated `using App.Core` to `using App` in 58 downstream files (19 PLang + 39 Tests)
6. Fixed relative `Core.` qualifiers in 13 files (Errors/, Context/, modules/mock/, Mapping/)
7. Fixed source generator `PLang.Generators/LazyParamsGenerator.cs` (3 hardcoded references)
8. Fixed v1 modules `PLang/Modules/PlangModule/Program.cs` and `PLang/Executor.cs`
9. Built PLang, PLang.Tests, PLangConsole — all 0 errors

### Final App/ structure
```
PLang/App/
├── Engine.cs, Property.cs, ErrorHandler.cs, Info.cs
├── Goals/ (Goal.cs, GoalMethods.cs, Goals.cs, GoalCall.cs)
├── Steps/ (Step.cs, StepMethods.cs, Steps.cs)
├── Actions/ (Action.cs, ActionMethods.cs, Actions.cs, IAction.cs)
├── Events/ (EventCollection.cs, Lifecycle.cs)
├── Cache/ (CacheSettings.cs, ICache.cs, MemoryStepCache.cs, StepCache.cs, StepCacheEntry.cs)
├── Execution/ (CallStack.cs, CallFrame.cs, DebugMode.cs, TestMode.cs)
├── Context/, Memory/, IO/, Errors/, Serialization/, Mapping/, Parsing/, Utility/, modules/
```
