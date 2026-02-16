# Plan: Restructure Runtime2/Core to Mirror Object Graph

## Context

Currently, all core entity types (Engine, Goal, Goals, Step, Steps, Action, Actions, Events, Cache, CallStack, etc.) live in a single flat `PLang/Runtime2/Core/` folder with namespace `PLang.Runtime2.Core`. The architecture-overview.md defines a clear object graph where `engine.Goals` navigates to goals, `engine.Events` navigates to events, etc. The C# folder structure should mirror this so that navigating the object graph in code corresponds to navigating the folder tree.

**~75 files** will be touched (26 Core files moving + 20 PLang consumer files + 39 test files + source generator + error files using relative namespace resolution).

---

## Target Folder Structure

```
PLang/Runtime2/
├── Engine.cs                     (ns: PLang.Runtime2)
├── Property.cs                   (ns: PLang.Runtime2)
├── Info.cs                       (ns: PLang.Runtime2)
│
├── Goals/                        ← engine.Goals
│   ├── Goal.cs                   (ns: PLang.Runtime2.Goals)
│   ├── GoalMethods.cs            (ns: PLang.Runtime2.Goals)
│   ├── Goals.cs                  (ns: PLang.Runtime2.Goals)
│   ├── GoalCall.cs               (ns: PLang.Runtime2.Goals)
│   └── Steps/                    ← goal.Steps
│       ├── Step.cs               (ns: PLang.Runtime2.Goals.Steps)
│       ├── StepMethods.cs        (ns: PLang.Runtime2.Goals.Steps)
│       ├── Steps.cs              (ns: PLang.Runtime2.Goals.Steps)
│       └── ErrorHandler.cs       (ns: PLang.Runtime2.Goals.Steps)
│
├── Actions/                      ← flat (actions used across the graph)
│   ├── Action.cs                 (ns: PLang.Runtime2.Actions)
│   ├── ActionMethods.cs          (ns: PLang.Runtime2.Actions)
│   ├── Actions.cs                (ns: PLang.Runtime2.Actions)
│   └── IAction.cs                (ns: PLang.Runtime2.Actions)
│
├── Events/                       ← engine.Events
│   ├── EventCollection.cs        (ns: PLang.Runtime2.Events)
│   └── Lifecycle.cs              (ns: PLang.Runtime2.Events)
│
├── Cache/                        ← engine.Cache
│   ├── ICache.cs                 (ns: PLang.Runtime2.Cache)
│   ├── MemoryStepCache.cs        (ns: PLang.Runtime2.Cache)
│   ├── StepCache.cs              (ns: PLang.Runtime2.Cache)
│   ├── StepCacheEntry.cs         (ns: PLang.Runtime2.Cache)
│   └── CacheSettings.cs          (ns: PLang.Runtime2.Cache)
│
├── Context/                      ← exists + additions from Core
│   ├── PLangContext.cs            (stays)
│   ├── Actor.cs                   (stays)
│   ├── EventScope.cs              (stays)
│   ├── CallStack.cs               (moved from Core)
│   ├── CallFrame.cs               (moved from Core)
│   ├── DebugMode.cs               (moved from Core)
│   └── TestMode.cs                (moved from Core)
│
├── Memory/       (no change)
├── IO/           (no change)
├── Errors/       (no change)
├── Serialization/ (no change)
├── Mapping/      (no change)
├── Utility/      (no change)
├── Parsing/      (no change)
└── modules/      (no change, Libraries.cs stays here)
```

**Name conflict strategy:** Class names stay as-is (Goals, Steps, Actions, Events). Use C# `using` aliases where the "class name = namespace name" ambiguity arises.

---

## Execution Plan

### Phase 0: Safety bridge
- Add `global using PLang.Runtime2.Core;` to a temporary `GlobalUsings.cs` in PLang project
- Add same to PLang.Tests project
- This ensures code compiles throughout the refactor as files move one group at a time

### Phase 1: Move Engine, Property, Info to Runtime2/ root
- `git mv` Engine.cs, Property.cs, Info.cs from Core/ to Runtime2/
- Update namespace to `PLang.Runtime2`
- Files: `Engine.cs`, `Property.cs`, `Info.cs`

### Phase 2: Create Events/ and move event files
- Create `PLang/Runtime2/Events/`
- Move `EventCollection.cs`, `Lifecycle.cs`
- Update namespace to `PLang.Runtime2.Events`

### Phase 3: Create Cache/ and move cache files
- Create `PLang/Runtime2/Cache/`
- Move `ICache.cs`, `MemoryStepCache.cs`, `StepCache.cs`, `StepCacheEntry.cs`, `CacheSettings.cs`
- Update namespace to `PLang.Runtime2.Cache`

### Phase 4: Move execution-tracking files to Context/
- Move `CallStack.cs`, `CallFrame.cs`, `DebugMode.cs`, `TestMode.cs` to existing Context/
- Update namespace to `PLang.Runtime2.Context`

### Phase 5: Create Goals/ and move goal files
- Create `PLang/Runtime2/Goals/`
- Move `Goal.cs`, `GoalMethods.cs`, `Goals.cs`, `GoalCall.cs`
- Update namespace to `PLang.Runtime2.Goals`

### Phase 6: Create Goals/Steps/ and move step files
- Create `PLang/Runtime2/Goals/Steps/`
- Move `Step.cs`, `StepMethods.cs`, `Steps.cs`, `ErrorHandler.cs`
- Update namespace to `PLang.Runtime2.Goals.Steps`

### Phase 7: Create Actions/ and move action files
- Create `PLang/Runtime2/Actions/`
- Move `Action.cs`, `ActionMethods.cs`, `Actions.cs`, `IAction.cs`
- Update namespace to `PLang.Runtime2.Actions`

### Phase 8: Update source generator
- File: `PLang.Generators/LazyParamsGenerator.cs`
- Change 3 hardcoded namespace strings:
  - `PLang.Runtime2.Core.Engine` → `PLang.Runtime2.Engine`
  - `PLang.Runtime2.Core.CallFrame` → `PLang.Runtime2.Context.CallFrame`

### Phase 9: Fix relative namespace references in Errors/
- 8 error files use `Core.Step`, `Core.CallFrame` via relative namespace (no explicit `using`)
- Add explicit `using PLang.Runtime2.Goals.Steps;` and `using PLang.Runtime2.Context;`
- Files: `IError.cs`, `Error.cs`, `ServiceError.cs`, `StepError.cs`, `GoalError.cs`, `ActionError.cs`, `ProgramError.cs`, `ValidationError.cs`

### Phase 10: Fix PLangContext.cs prefix references
- Has ~25 `Core.` prefixed references (Core.Lifecycle, Core.Goal, Core.Step, Core.Action, Core.EventType)
- Replace with new namespace imports + handle any ambiguity with aliases

### Phase 11: Fix all `using PLang.Runtime2.Core` references
- ~20 PLang source files: replace with specific new namespace imports
- ~39 test files: same treatment
- `PLang/Modules/PlangModule/Program.cs`: update `using Actions = PLang.Runtime2.Core.Actions;` alias

### Phase 12: Remove global using bridge
- Remove temporary `GlobalUsings.cs` from both projects
- Delete empty `Core/` folder
- Verify clean build

### Phase 13: Update documentation
- Update architecture-overview.md, engine.md, goals-steps.md, etc. to reflect new paths

---

## Key Files to Modify

| File | Change |
|------|--------|
| `PLang.Generators/LazyParamsGenerator.cs` | 3 hardcoded namespace strings |
| `PLang/Runtime2/Context/PLangContext.cs` | ~25 `Core.` prefix references |
| `PLang/Runtime2/Mapping/GoalMapper.cs` | ~15 `Core.` prefix references |
| `PLang/Runtime2/Errors/*.cs` (8 files) | Relative `Core.Step`/`Core.CallFrame` references |
| `PLang/Modules/PlangModule/Program.cs` | Using alias for Actions |
| All 26 files in Core/ | Namespace declarations change |
| ~20 Runtime2 consumer files | `using` statements update |
| ~39 test files | `using` statements update |

---

## Verification

1. `dotnet build PLang/PLang.csproj` — library compiles
2. `dotnet build PlangConsole/PlangConsole.csproj` — executable compiles
3. `dotnet build PLang.Tests/PLang.Tests.csproj` — tests compile
4. `dotnet run --project PLang.Tests` — all tests pass
5. `plang p build` on a test project — builder still works (source generator correct)
6. `plang p` on Tests/HelloWorld — runtime still works
7. Verify Core/ folder is empty and deleted
