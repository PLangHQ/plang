# Restructure Runtime2/Core/ Plan

## Goal
Move 26 files from flat `PLang/Runtime2/Core/` into subfolders that mirror the object graph.

## Approach
**Folder-only move, keep namespace `PLang.Runtime2.Core`.**
- C# does not require namespace-to-folder matching
- Zero downstream using changes needed (58 files untouched)
- Build stays green immediately after moves
- git mv preserves file history

## Target Structure

```
PLang/Runtime2/Core/
├── Engine.cs              (stays - root)
├── Property.cs            (stays - engine's key-value store)
├── ErrorHandler.cs        (stays - error config struct)
├── Info.cs                (stays - simple data)
├── Goals/
│   ├── Goal.cs
│   ├── GoalMethods.cs
│   ├── Goals.cs
│   └── GoalCall.cs
├── Steps/
│   ├── Step.cs
│   ├── StepMethods.cs
│   └── Steps.cs
├── Actions/
│   ├── Action.cs
│   ├── ActionMethods.cs
│   ├── Actions.cs
│   └── IAction.cs
├── Events/
│   ├── EventCollection.cs
│   └── Lifecycle.cs
├── Cache/
│   ├── CacheSettings.cs
│   ├── ICache.cs
│   ├── MemoryStepCache.cs
│   ├── StepCache.cs
│   └── StepCacheEntry.cs
└── Execution/
    ├── CallStack.cs
    ├── CallFrame.cs
    ├── DebugMode.cs
    └── TestMode.cs
```

## Execution Groups

### Group A: Create subfolders
Create Goals/, Steps/, Actions/, Events/, Cache/, Execution/ under Core/

### Group B: Move files (git mv)
Move each file to its target subfolder using git mv

### Group C: Build verification
Run dotnet build on PLang and PLang.Tests to verify zero breakage

### Group D: Commit
Single commit with descriptive message

## File Categorization (22 files moving, 4 staying)

**Stays in Core/ (4):** Engine.cs, Property.cs, ErrorHandler.cs, Info.cs
**→ Goals/ (4):** Goal.cs, GoalMethods.cs, Goals.cs, GoalCall.cs
**→ Steps/ (3):** Step.cs, StepMethods.cs, Steps.cs
**→ Actions/ (4):** Action.cs, ActionMethods.cs, Actions.cs, IAction.cs
**→ Events/ (2):** EventCollection.cs, Lifecycle.cs
**→ Cache/ (5):** CacheSettings.cs, ICache.cs, MemoryStepCache.cs, StepCache.cs, StepCacheEntry.cs
**→ Execution/ (4):** CallStack.cs, CallFrame.cs, DebugMode.cs, TestMode.cs
