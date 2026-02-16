# v13 Summary — Restructure Runtime2/Core/

Reorganized the flat 26-file `PLang/Runtime2/Core/` folder into 6 subfolders mirroring the object graph:
- **Goals/** (4 files) — Goal, GoalMethods, Goals, GoalCall
- **Steps/** (3 files) — Step, StepMethods, Steps
- **Actions/** (4 files) — Action, ActionMethods, Actions, IAction
- **Events/** (2 files) — EventCollection, Lifecycle
- **Cache/** (5 files) — CacheSettings, ICache, MemoryStepCache, StepCache, StepCacheEntry
- **Execution/** (4 files) — CallStack, CallFrame, DebugMode, TestMode
- **Root** (4 files stay) — Engine, Property, ErrorHandler, Info

Namespace kept as `PLang.Runtime2.Core` — zero downstream changes. All 3 projects build clean.
