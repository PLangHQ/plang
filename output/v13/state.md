# Session v13 State

## Status: Complete
## Task: Restructure Runtime2/Core/ to mirror object graph

### What was done
- Created branch `restructure-core-folders` from `runtime2`
- Created 6 subfolders: Goals/, Steps/, Actions/, Events/, Cache/, Execution/
- Moved 22 files using `git mv` (preserves history)
- Kept namespace `PLang.Runtime2.Core` unchanged (zero downstream impact)
- Built PLang, PLang.Tests, PLangConsole — all 0 errors

### Files staying in Core/ (4)
- Engine.cs, Property.cs, ErrorHandler.cs, Info.cs

### Files moved (22)
- Goals/: Goal.cs, GoalMethods.cs, Goals.cs, GoalCall.cs
- Steps/: Step.cs, StepMethods.cs, Steps.cs
- Actions/: Action.cs, ActionMethods.cs, Actions.cs, IAction.cs
- Events/: EventCollection.cs, Lifecycle.cs
- Cache/: CacheSettings.cs, ICache.cs, MemoryStepCache.cs, StepCache.cs, StepCacheEntry.cs
- Execution/: CallStack.cs, CallFrame.cs, DebugMode.cs, TestMode.cs

### What's next
- Commit and push
- Optional follow-up: change namespaces to match folders (PLang.Runtime2.Core.Goals, etc.) — would require updating 58 files
