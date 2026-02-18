# v14 State -- Rename All Primary Classes to @this

## Status: Phases 1-6 COMPLETE, Phase 7 remaining

### Completed Phases
- **Phase 1**: Leaf singletons (CallStack, Debug, Test, Properties) → @this (committed 8793f09d)
- **Phase 2**: Events subsystem (EngineEvents, Lifecycle, Bindings, EventBinding) → @this (committed 0996a19a)
- **Phase 3**: Libraries subsystem (EngineLibraries, Library) → @this (committed bf4a1e82)
- **Phase 4**: Channels subsystem (EngineChannels, Channel, EngineSerializers) → @this (committed de84a152)
- **Phase 5**: Entity hierarchy + R2 alias cleanup (committed 4f708e0f)
  - Renamed: EngineGoals, Goal, GoalSteps, Step, StepActions, Action → @this
  - Un-shared all namespaces: each folder gets own namespace matching path
  - **Removed ALL R2{Name} per-file aliases** → replaced with ChildNamespace.@this
  - Fixed 43 files including 16 test files
- **Phase 6**: Engine root class → @this (committed in this session)
  - Renamed `class Engine` → `class @this` in Engine/this.cs
  - Updated 14 files within Engine.* namespace: `Engine` type → `Engine.@this`
  - Updated IClass.cs, ICodeGenerated.cs: `EngineType` alias → `PLang.Runtime2.Engine.@this`
  - Updated LazyParamsGenerator.cs: FQN string literals + engine-resolvable check
  - Updated Executor.cs: `new Runtime2.Engine.Engine(...)` → `new Runtime2.Engine.@this(...)`
  - Added global alias in PLang.Tests/GlobalUsings.cs: `Engine = PLang.Runtime2.Engine.@this`
  - No global alias in PLang project (namespace shadows it)

### Key Design Decision: Engine.@this
A global alias `Engine` can't work in the PLang project because the namespace `PLang.Runtime2.Engine` shadows it from every file in `PLang.Runtime2.*`. Instead, all files use `Engine.@this` — the same ChildNamespace.@this pattern used for Channel.@this, Library.@this, Goal.@this, etc.

In the test project, the global alias works because test namespaces (`PLang.Tests.Runtime2.*`) don't have an `Engine` namespace segment.

### Remaining
- **Phase 7**: Documentation updates

### Build Status
- PLang.csproj: 0 errors
- PLang.Tests.csproj: 0 errors
- All 1167 tests pass
