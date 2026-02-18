# Runtime2 Engine — Folder Restructure Handover

## Objective

Restructure the PLang Runtime2 engine so that **folder paths match namespaces exactly**, and every folder's primary class is named `this.cs`. The goal is a predictable, mechanical mapping that an LLM can navigate without ambiguity.

## Core Rules

1. **Namespace = folder path.** If a class lives in namespace `Engine.Goals.Goal`, the file is at `Engine/Goals/Goal/this.cs`.
2. **`this.cs` is the primary concept of a folder.** It's the thing the folder IS. All other files in the folder are satellites.
3. **Plural = collection, Singular = entity.** Collections (smart collections, registries) live in plural folders. The entity they contain lives in a singular subfolder.
4. **Nest when there's a collection/entity relationship.** Always. Tree depth is not a concern — predictability is.
5. **Singular folders for standalone concepts.** Things that aren't collections (`Debug`, `Test`, `Cache`, `CallStack`, `Context`, `Memory`) stay singular.
6. **Shortcuts are fine.** `engine.Step` can be a shortcut to `engine.Goals.Steps.Step`. The canonical location is in the nested path. Convenience accessors on Engine or intermediate classes can point there.

## Target File Structure

```
Engine/
├── this.cs                        → Engine (root orchestrator, sealed, IAsyncDisposable)
├── Info.cs                        → Version/build info
│
├── Goals/
│   ├── this.cs                    → EngineGoals (smart collection, lazy disk loading)
│   └── Goal/
│       ├── this.cs                → Goal entity (properties)
│       ├── Methods.cs             → Goal runtime methods (Load, RunAsync)
│       ├── GoalCall.cs            → Strongly-typed goal reference (name, parameters)
│       └── Steps/
│           ├── this.cs            → GoalSteps : List<Step> (smart collection)
│           └── Step/
│               ├── this.cs        → Step entity (properties)
│               ├── Methods.cs     → Step runtime methods (Load, RunAsync)
│               ├── ErrorHandler.cs → Step error configuration
│               ├── CacheSettings.cs → Step cache configuration
│               ├── StepCache.cs   → Step-level cache wrapper
│               └── Actions/
│                   ├── this.cs    → StepActions : List<Action> (smart collection)
│                   └── Action/
│                       ├── this.cs → Action entity (properties)
│                       └── Methods.cs → Action runtime methods (RunAsync)
│
├── Context/
│   ├── this.cs                    → PLangContext (per-request: MemoryStack, CallStack, Actor, EventScope)
│   ├── Actor.cs                   → Identity (System/Service/User)
│   └── EventScope.cs              → Event scope wrapper (owns EngineEvents)
│
├── Memory/
│   ├── this.cs                    → MemoryStack (ConcurrentDictionary, case-insensitive)
│   ├── Data.cs                    → Universal container + Type class (value, error, success)
│   ├── Properties.cs              → Properties : IList<Data>
│   ├── IValueNavigator.cs         → Navigation interface for dot-paths
│   ├── PlangTypeConverter.cs      → Type conversion utilities
│   ├── TString.cs                 → Translatable string type
│   └── TypeJsonConverter.cs       → JSON converter for Type
│
├── Events/
│   ├── this.cs                    → EngineEvents (global event collection + dispatch)
│   ├── EventType.cs               → Enum (BeforeGoal, AfterStep, etc.)
│   └── Lifecycle/
│       ├── this.cs                → Lifecycle (per-entity Before/After)
│       └── Bindings/
│           ├── this.cs            → Bindings (ordered event binding collection)
│           └── Binding/
│               └── this.cs        → EventBinding (handler binding with pattern matching)
│
├── Errors/
│   ├── this.cs                    → IError + Error base implementation
│   ├── GoalError.cs               → Goal-level errors
│   ├── StepError.cs               → Step-level errors
│   ├── ActionError.cs             → Action-level errors
│   ├── ServiceError.cs            → External service errors
│   ├── ProgramError.cs            → Program-level errors
│   ├── ValidationError.cs         → Validation errors
│   ├── AssertionError.cs          → Test assertion errors
│   ├── ErrorCategory.cs           → Error categorization
│   └── Exceptions.cs              → Runtime2Exception types
│
├── Channels/
│   ├── this.cs                    → EngineChannels (channel manager, named I/O routing)
│   └── Channel/
│       └── this.cs                → Channel (stream-backed, carries Data)
│
├── Serializers/
│   ├── this.cs                    → EngineSerializers (content-type routing registry)
│   ├── View.cs                    → [Store], [LlmBuilder], [Debug], [Default] attributes
│   ├── ViewPropertyFilter.cs      → View-based property filtering
│   └── Serializer/
│       ├── this.cs                → ISerializer interface
│       ├── JsonStreamSerializer.cs → System.Text.Json implementation
│       └── TextStreamSerializer.cs → Plain text implementation
│
├── Libraries/
│   ├── this.cs                    → EngineLibraries (smart collection, handler resolution)
│   └── Library/
│       └── this.cs                → Library (single assembly's handlers)
│
├── Properties/
│   ├── this.cs                    → EngineProperties (collection/registry, key-value + GoalCall resolution)
│   └── Property/
│       └── this.cs                → Property (individual key-value item with behavior)
│
├── Cache/
│   ├── this.cs                    → ICache interface
│   ├── StepCacheEntry.cs          → Cache entry type
│   └── MemoryStepCache.cs         → In-memory ICache implementation
│
├── CallStack/
│   ├── this.cs                    → CallStack (opt-in execution tracking)
│   ├── CallFrame.cs               → Stack frame with ExecutionPhase enum
│   ├── ExecutedStep.cs            → Record of an executed step
│   └── SerializableCallStack.cs   → Serializable call stack/frame DTOs
│
├── Debug/
│   └── this.cs                    → EngineDebug (debug mode controller)
│
├── Test/
│   └── this.cs                    → EngineTest (test runner)
│
└── Utility/
    ├── TypeMapping.cs             → PLang type names + MIME → CLR types + ConvertTo
    ├── AppData.cs                 → Application data utilities
    ├── GoalMapper.cs              → Building.Model → Runtime2 conversion
    └── PrParser.cs                → .pr file parser
```

## Current → Target File Mapping

This table maps every file from the current flat structure to its new location.

| Current path | New path |
|---|---|
| `Engine/Engine.cs` | `Engine/this.cs` |
| `Engine/Info.cs` | `Engine/Info.cs` |
| `Engine/EngineGoals.cs` | `Engine/Goals/this.cs` |
| `Engine/Goal.cs` | `Engine/Goals/Goal/this.cs` |
| `Engine/Goal.Methods.cs` | `Engine/Goals/Goal/Methods.cs` |
| `Engine/GoalCall.cs` | `Engine/Goals/Goal/GoalCall.cs` |
| `Engine/GoalSteps.cs` | `Engine/Goals/Goal/Steps/this.cs` |
| `Engine/Step.cs` | `Engine/Goals/Goal/Steps/Step/this.cs` |
| `Engine/Step.Methods.cs` | `Engine/Goals/Goal/Steps/Step/Methods.cs` |
| `Engine/ErrorHandler.cs` | `Engine/Goals/Goal/Steps/Step/ErrorHandler.cs` |
| `Engine/CacheSettings.cs` | `Engine/Goals/Goal/Steps/Step/CacheSettings.cs` |
| `Engine/StepCache.cs` | `Engine/Goals/Goal/Steps/Step/StepCache.cs` |
| `Engine/StepActions.cs` | `Engine/Goals/Goal/Steps/Step/Actions/this.cs` |
| `Engine/Action.cs` | `Engine/Goals/Goal/Steps/Step/Actions/Action/this.cs` |
| `Engine/Action.Methods.cs` | `Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs` |
| `Engine/IAction.cs` | `Engine/Goals/Goal/Steps/Step/Actions/Action/IAction.cs` |
| `Engine/Context/PLangContext.cs` | `Engine/Context/this.cs` |
| `Engine/Context/Actor.cs` | `Engine/Context/Actor.cs` |
| `Engine/Context/EventScope.cs` | `Engine/Context/EventScope.cs` |
| `Engine/Memory/MemoryStack.cs` | `Engine/Memory/this.cs` |
| `Engine/Memory/Data.cs` | `Engine/Memory/Data.cs` |
| `Engine/Memory/Properties.cs` | `Engine/Memory/Properties.cs` |
| `Engine/Memory/IValueNavigator.cs` | `Engine/Memory/IValueNavigator.cs` |
| `Engine/Memory/PlangTypeConverter.cs` | `Engine/Memory/PlangTypeConverter.cs` |
| `Engine/Memory/TString.cs` | `Engine/Memory/TString.cs` |
| `Engine/Memory/TypeJsonConverter.cs` | `Engine/Memory/TypeJsonConverter.cs` |
| `Engine/EngineEvents.cs` | `Engine/Events/this.cs` |
| `Engine/EventType.cs` | `Engine/Events/EventType.cs` |
| `Engine/Lifecycle.cs` | `Engine/Events/Lifecycle/this.cs` |
| `Engine/Bindings.cs` | `Engine/Events/Lifecycle/Bindings/this.cs` |
| `Engine/EventBinding.cs` | `Engine/Events/Lifecycle/Bindings/Binding/this.cs` |
| `Engine/Channels/EngineChannels.cs` | `Engine/Channels/this.cs` |
| `Engine/Channels/Channel.cs` | `Engine/Channels/Channel/this.cs` |
| `Engine/Channels/ChannelData.cs` | **REMOVED** — channels carry `Data` directly |
| `Engine/Serializers/EngineSerializers.cs` | `Engine/Serializers/this.cs` |
| `Engine/Serializers/ISerializer.cs` | `Engine/Serializers/Serializer/this.cs` |
| `Engine/Serializers/JsonStreamSerializer.cs` | `Engine/Serializers/Serializer/JsonStreamSerializer.cs` |
| `Engine/Serializers/TextStreamSerializer.cs` | `Engine/Serializers/Serializer/TextStreamSerializer.cs` |
| `Engine/Serializers/View.cs` | `Engine/Serializers/View.cs` |
| `Engine/Serializers/ViewPropertyFilter.cs` | `Engine/Serializers/ViewPropertyFilter.cs` |
| `Engine/EngineLibraries.cs` | `Engine/Libraries/this.cs` |
| `Engine/Library.cs` | `Engine/Libraries/Library/this.cs` |
| `Engine/EngineProperty.cs` | `Engine/Properties/this.cs` |
| *(new)* | `Engine/Properties/Property/this.cs` |
| `Engine/ICache.cs` | `Engine/Cache/this.cs` |
| `Engine/StepCacheEntry.cs` | `Engine/Cache/StepCacheEntry.cs` |
| `Engine/MemoryStepCache.cs` | `Engine/Cache/MemoryStepCache.cs` |
| `Engine/CallStack.cs` | `Engine/CallStack/this.cs` |
| `Engine/CallFrame.cs` | `Engine/CallStack/CallFrame.cs` |
| `Engine/ExecutedStep.cs` | `Engine/CallStack/ExecutedStep.cs` |
| `Engine/SerializableCallStack.cs` | `Engine/CallStack/SerializableCallStack.cs` |
| `Engine/EngineDebug.cs` | `Engine/Debug/this.cs` |
| `Engine/EngineTesting.cs` | `Engine/Test/this.cs` |
| `Engine/Errors/*.cs` | `Engine/Errors/*.cs` (same structure) |
| `Engine/Utility/TypeMapping.cs` | `Engine/Utility/TypeMapping.cs` |
| `Engine/Utility/AppData.cs` | `Engine/Utility/AppData.cs` |
| `Engine/Mapping/GoalMapper.cs` | `Engine/Utility/GoalMapper.cs` |
| `Engine/Parsing/PrParser.cs` | `Engine/Utility/PrParser.cs` |

## Namespace Updates

Every file's namespace must match its folder path. Examples:

| File | Namespace |
|---|---|
| `Engine/this.cs` | `Engine` |
| `Engine/Goals/this.cs` | `Engine.Goals` |
| `Engine/Goals/Goal/this.cs` | `Engine.Goals.Goal` |
| `Engine/Goals/Goal/Steps/this.cs` | `Engine.Goals.Goal.Steps` |
| `Engine/Goals/Goal/Steps/Step/this.cs` | `Engine.Goals.Goal.Steps.Step` |
| `Engine/Goals/Goal/Steps/Step/Actions/this.cs` | `Engine.Goals.Goal.Steps.Step.Actions` |
| `Engine/Goals/Goal/Steps/Step/Actions/Action/this.cs` | `Engine.Goals.Goal.Steps.Step.Actions.Action` |
| `Engine/Events/Lifecycle/Bindings/Binding/this.cs` | `Engine.Events.Lifecycle.Bindings.Binding` |
| `Engine/Context/this.cs` | `Engine.Context` |
| `Engine/Memory/this.cs` | `Engine.Memory` |

## Engine Property Map

These are the properties on the Engine class and where they resolve to:

| Engine property | Type | Canonical path |
|---|---|---|
| `engine.Goals` | `EngineGoals` | `Engine/Goals/this.cs` |
| `engine.Context` | `PLangContext` | `Engine/Context/this.cs` |
| `engine.Channels` | `EngineChannels` | `Engine/Channels/this.cs` |
| `engine.Events` | `EngineEvents` | `Engine/Events/this.cs` |
| `engine.Serializers` | `EngineSerializers` | `Engine/Serializers/this.cs` |
| `engine.Libraries` | `EngineLibraries` | `Engine/Libraries/this.cs` |
| `engine.Properties` | `EngineProperties` | `Engine/Properties/this.cs` |
| `engine.Cache` | `ICache` | `Engine/Cache/this.cs` |
| `engine.CallStack` | `CallStack` | `Engine/CallStack/this.cs` |
| `engine.Debug` | `EngineDebug` | `Engine/Debug/this.cs` |
| `engine.Test` | `EngineTest` | `Engine/Test/this.cs` |
| `engine.FileSystem` | `IPLangFileSystem` | *(interface — location TBD, not currently in Engine/)* |

## Shortcut Accessors

These are convenience properties that point to deeply nested canonical locations:

| Shortcut | Resolves to |
|---|---|
| `engine.Step` | `engine.Goals.Goal.Steps.Step` (current step in context) |
| `engine.Goal` | `engine.Goals.Goal` (current goal in context) |
| `engine.Action` | `engine.Goals.Goal.Steps.Step.Actions.Action` (current action in context) |

## Naming Conventions

| Pattern | Folder | `this.cs` contains | Example |
|---|---|---|---|
| Collection | Plural name | `Engine{Name}` class | `Goals/this.cs` → `EngineGoals` |
| Entity | Singular name | Entity class | `Goal/this.cs` → `Goal` |
| Standalone | Singular name | Controller/concept | `Debug/this.cs` → `EngineDebug` |
| Utility | `Utility` | No `this.cs` | Individual helper files |

## Design Decisions

- **`ChannelData.cs` removed.** Channels carry `Data` (from Memory) directly. No wrapper needed.
- **`Testing` renamed to `Test`.** `engine.Test` matches `engine.Debug` — both are singular controllers.
- **`Property` renamed to `Properties`.** `engine.Properties` matches the plural collection pattern.
- **`Mapping/` and `Parsing/` merged into `Utility/`.** They're support code, not engine-owned concepts.
- **`StepCache.cs`, `CacheSettings.cs`, `ErrorHandler.cs`** moved under `Step/`. They're step-owned configuration.
- **`ICache`, `StepCacheEntry`, `MemoryStepCache`** stay in `Engine/Cache/`. They're engine-level cache infrastructure.
- **`Lifecycle`, `Bindings`, `Binding`** nested under `Events/`. The lifecycle system is owned by Events conceptually, even though Goal/Step/Action reference it.
- **`IAction.cs`** moves to `Actions/Action/`. It defines the action interface — belongs with the entity.

## What NOT to change

- The `actions/` folder (lowercase, at the Runtime2 root) containing handler implementations (`variable/`, `file/`, `output/`, etc.) is separate from the Engine structure. Don't restructure it as part of this task.
- File contents and class logic stay the same. This is purely a move + namespace update.
- The builder pipeline (v1 engine) is untouched.
