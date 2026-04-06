# Task: Law of Names — App Restructuring (Phases 1-4)

## What this is

A large mechanical refactoring of PLang App. Every file moves to a new folder, gets a new namespace, and ~11 classes get renamed. **Zero behavior change** in phases 1-3. Small behavioral changes in phase 4.

All design decisions are locked. No ambiguity. This is pure execution.

## Context files provided

- `context/tree-map.md` — Complete current→proposed file mapping (the blueprint)
- `context/migration-plan.md` — Phase-by-phase operations with exact namespace mappings
- `context/engine-current.cs` — Current Engine.cs (the root object you'll be modifying)
- `context/namespace-map.txt` — Simple search-and-replace table for Phase 1

## What you need to do

### Phase 1: Folder Restructure + Namespace Migration (one atomic commit)

1. Create the folder structure under `PLang/App/Engine/`
2. Move all files to their new locations per the tree map
3. Update namespace declarations in every moved file
4. Update `using` statements in ALL files (production + tests + PlangModule)
5. Update LazyParamsGenerator.cs — 15 hardcoded namespace strings
6. Update `Library.Discover("App.modules")` → `Library.Discover("App.Engine.modules")`
7. Build and fix any remaining references

**Namespace search-and-replace** (order matters — longest match first):
```
App.Memory.Navigators → App.Engine.Variables.Navigators
App.Core              → App.Engine
App.Context           → App.Engine.Context
App.Memory            → App.Engine.Variables
App.IO                → App.Engine.Channels
App.Errors            → App.Engine.Errors
App.Serialization     → App.Engine.Serializers
App.Utility           → App.Engine.Utility
App.Parsing           → App.Engine.Parsing
App.Mapping           → App.Engine.Mapping
```

**modules/ split:**
- Infrastructure files (IClass, ICodeGenerated, Libraries, Library, ActionAttribute, DefaultAttribute, VariableNameAttribute, IContext) → `Engine/Libraries/` namespace `App.Engine.Libraries`
- Handler subfolders (variable/, file/, etc.) → `Engine/modules/` namespace `App.Engine.modules`

**Data/Type split:**
- `Memory/Data.cs` contains both `Data` and `Type` classes
- `Type` moves to `Engine/Type.cs` with namespace `App.Engine`
- `Data`, `Data<T>`, `DynamicData` move to `Engine/Data.cs` with namespace `App.Engine`
- Remaining Memory files move to `Engine/Memory/`

**EventScope move:**
- `Context/EventScope.cs` → `Engine/Events/EventScope.cs` with namespace `App.Engine.Events`

**External files to update:**
- `PLang.Generators/LazyParamsGenerator.cs` — 15 namespace strings
- `PLang/Modules/PlangModule/Program.cs` — 4 using statements + ~32 references
- All 55 test files in `PLang.Tests/App/` — ~322 references

**Verification:** `dotnet build PLang.sln` && `dotnet run --project PLang.Tests`

### Phase 2: File Organization (separate commit)

1. Split `EventCollection.cs` into: `Events.cs` (class), `EventBinding.cs`, `EventType.cs`
2. Split `CallStack.cs` — move `SerializableCallStack` + `SerializableCallFrame` to `SerializableCallStack.cs`
3. Rename partial files: `GoalMethods.cs` → `Goal.Methods.cs`, `StepMethods.cs` → `Step.Methods.cs`, `ActionMethods.cs` → `Action.Methods.cs`

**Verification:** `dotnet build PLang.sln` && `dotnet run --project PLang.Tests`

### Phase 3: Convention Renames (separate commit)

Rename these 11 classes and update ALL references:

| Current | New |
|---------|-----|
| `Goals` | `EngineGoals` |
| `Steps` | `GoalSteps` |
| `Actions` | `StepActions` |
| `Channels` (IO class) | `EngineChannels` |
| `Property` | `EngineProperty` |
| `Events` (event system class) | `EngineEvents` |
| `SerializerRegistry` | `EngineSerializers` |
| `Libraries` | `EngineLibraries` |
| `Library` | stays `Library` (not convention-wired) |
| `DebugMode` | `EngineDebug` |
| `TestMode` | `EngineTesting` |

**Watch out for:**
- `Actions` is aliased in PlangModule: `using Actions = App.Core.Actions` — update alias
- `Events` is a common word — only rename the class, not `EventType`, `EventBinding`, etc.
- `Channels` — only rename the collection class, not the `Channel` entity

**Verification:** `dotnet build PLang.sln` && `dotnet run --project PLang.Tests`

### Phase 4: New Convention Types (separate commit)

1. **Create `EngineCache`** in `Engine/Cache/this.cs`:
   - Wraps `ICache`, delegates `GetAsync`/`SetAsync`/`RemoveAsync`
   - Has `Implementation` property to swap the backing cache
   - Default is `MemoryStepCache`

2. **Convert `EngineDebug` from static to instance**:
   - Remove `static` keyword
   - Add constructor that takes Engine (or navigates to it)
   - `Apply(Engine engine, object debugValue)` → instance method `Enable(object debugValue)`

3. **Convert `EngineTesting` from static to instance**:
   - Remove `static` keyword
   - `RunAsync(Engine engine, ct)` → `RunAsync(ct)` (engine stored as field)

4. **Update Engine**:
   - `ICache Cache { get; set; }` → `EngineCache Cache { get; }`
   - Add `EngineDebug Debug { get; }` property
   - Add `EngineTesting Testing { get; }` property
   - Update constructor
   - Update all callers

**Verification:** Full build + tests + `plang p !debug` + `plang p !test`

## Important constraints

- **NEVER use System.IO** — use `fileSystem.*`
- **NEVER edit .pr files**
- **NEVER weaken types to `object`**
- Each phase is a separate commit
- Each phase must build and pass ALL tests before proceeding
- Phase 1 is one atomic commit — do NOT split it into sub-commits
- If build fails after Phase 1, fix the missing using/namespace references — don't revert

## Blast radius

| Area | Files | References |
|------|-------|-----------|
| App production | ~165 | Every namespace declaration |
| LazyParamsGenerator | 1 | 15 hardcoded strings |
| PlangModule/Program.cs | 1 | ~32 references |
| Test files | 55 | ~322 references |
| **Total** | ~222 | ~500+ string replacements |
