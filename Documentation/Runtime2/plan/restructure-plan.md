# Runtime2/Core/ Folder Restructure

## Status: Implemented (branch: restructure-core-folders)

## What Changed
Reorganized flat `PLang/Runtime2/Core/` (26 files) into subfolders mirroring the object graph.

## New Structure
```
PLang/Runtime2/Core/
├── Engine.cs              (root orchestrator)
├── Property.cs            (engine key-value store)
├── ErrorHandler.cs        (error config)
├── Info.cs                (key/message data)
├── Goals/
│   ├── Goal.cs            (goal data)
│   ├── GoalMethods.cs     (goal behavior: Load, RunAsync)
│   ├── Goals.cs           (smart collection, lookup)
│   └── GoalCall.cs        (typed goal reference)
├── Steps/
│   ├── Step.cs            (step data)
│   ├── StepMethods.cs     (step behavior: Load, RunAsync)
│   └── Steps.cs           (smart collection)
├── Actions/
│   ├── Action.cs          (action data)
│   ├── ActionMethods.cs   (action behavior: RunAsync)
│   ├── Actions.cs         (smart collection)
│   └── IAction.cs         (interface)
├── Events/
│   ├── EventCollection.cs (EventType, EventBinding, Bindings)
│   └── Lifecycle.cs       (Before/After bindings)
├── Cache/
│   ├── CacheSettings.cs   (cache config)
│   ├── ICache.cs          (pluggable interface)
│   ├── MemoryStepCache.cs (default implementation)
│   ├── StepCache.cs       (cache runner)
│   └── StepCacheEntry.cs  (cached snapshot)
└── Execution/
    ├── CallStack.cs       (execution tracking)
    ├── CallFrame.cs       (frame state)
    ├── DebugMode.cs       (debug output driver)
    └── TestMode.cs        (test runner)
```

## Design Decision
- **Folder-only move** — namespace stays `PLang.Runtime2.Core` for all files
- Zero downstream `using` changes needed (58 files untouched)
- git mv preserves file history
- Future option: change namespaces to `PLang.Runtime2.Core.Goals` etc.
