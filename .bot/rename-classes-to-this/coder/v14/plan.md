# Runtime2 Engine Folder Restructure — Implementation Plan

## Phase 1: GlobalUsings + Entity Hierarchy
1. Create folder tree: Goals/Goal/Steps/Step/Actions/Action/
2. git mv 14 entity files to target locations
3. Update namespace declarations in all 14 files
4. Create GlobalUsings.cs in PLang and PLang.Tests (aliases for Goal, Step, GoalCall, GoalSteps, StepActions, CallStack, CallFrame, EventBinding, Lifecycle, Bindings)
5. Add per-file `using Action = ...` alias in ~5 PLang files and ~15 test files
6. Build + fix iteratively
7. Commit

## Phase 2: Events Hierarchy
1. Create Events/Lifecycle/Bindings/Binding/
2. git mv 5 event files
3. Update namespace declarations
4. Build + fix
5. Commit

## Phase 3: Standalone Concepts
1. Create Libraries/Library/, Properties/Property/, Cache/, CallStack/, Debug/, Test/
2. git mv 13 files
3. Rename: EngineProperty→EngineProperties, EngineTesting→EngineTest
4. Update LazyParamsGenerator hardcoded CallFrame namespace
5. Build + fix
6. Commit

## Phase 4: Channels + Utility + Root Renames
1. Channels: EngineChannels.cs→this.cs, Channel.cs→Channel/this.cs, serializers→Serializer/
2. Utility: move GoalMapper.cs, PrParser.cs
3. Root: Engine.cs→this.cs
4. Build + fix
5. Commit

## Phase 5: Documentation + Final Cleanup
1. Update README.md, good_to_know.md, CLAUDE.md
2. Final grep verification
3. Commit
