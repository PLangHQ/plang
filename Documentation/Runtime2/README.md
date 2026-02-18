# PLang Runtime2 — Architecture Overview

Runtime2 is PLang's second-generation execution engine. It replaces the v1 module system with an object-based action handler architecture, a universal `Data` result type, and source-generated lazy parameter resolution.

## The Object Graph

Engine is the root — everything hangs off it:

```
Engine (sealed, IAsyncDisposable)
├── EngineLibraries  (built-in [0] + external DLLs, handler resolution)
├── Goals            (EngineGoals — goal collection with lazy disk loading)
├── FileSystem       (IPLangFileSystem — abstracted filesystem)
├── Channels         (EngineChannels — channel-based I/O routing)
│   └── Serializers  (EngineSerializers — content-type based)
├── Events           (EngineEvents — global event collection)
├── Cache            (ICache — pluggable step cache)
├── Property         (EngineProperty — key-value store with GoalCall resolution)
├── Debug            (EngineDebug — debug mode controller)
├── Testing          (EngineTesting — test runner)
└── Actors (lazy)
    ├── System       (internal engine operations)
    ├── Service      (external service operations)
    └── User         (end user operations)
         └── Context → PLangContext
                        ├── MemoryStack   (variable storage)
                        ├── CallStack     (execution tracking)
                        ├── System/User   (EventScope → EngineEvents)
                        └── Actor         (identity)
```

## Entity Hierarchy: Goal → GoalSteps → StepActions

```
Goal (one .pr file)
 └── GoalSteps : List<Step>  (smart collection, owns Load)
      └── StepActions : List<Action>  (smart collection, owns RunAsync)
           └── Action → resolves to a Handler (e.g. variable/set → SetHandler)
```

Each level calls `.Load()` then `.RunAsync()`. Events fire before/after each phase via `Lifecycle` (Before/After `Bindings`).

## Execution Flow

```
plang p Start.goal
  → Engine loads .build/start.pr (JSON → Goal)
  → Engine.RunGoalAsync(goal, context)
    → goal.RunAsync() → for each Step:
      → step.RunAsync() → Actions.RunAsync() → for each Action:
        → EngineLibraries.GetCodeGenerated(module, action) finds handler
        → Source-generated code resolves %variables% in params
        → handler.Run() executes, returns Data
        → Return values stored in MemoryStack
```

## Design Principles

**Object-Based Pattern (OBP)**:
1. **Behavior on owner** — `GoalSteps.Load()` loads steps, not external code
2. **Navigate, don't pass** — pass Engine/Context, let caller reach what it needs
3. **Keep object references** — store `Step`, not `step.Text`; store `Goal`, not `goal.Name`
4. **Per-request state is a parameter** — PLangContext never cached on shared objects (Goal, Step)
5. **Smart collections** — GoalSteps, StepActions extend `List<T>` and own domain operations (Load, RunAsync)

**Strongly typed**: TypeMapping maps PLang type names to CLR types. Never weaken to `object` without explicit reason.

**Universal result type**: `Data` wraps success/failure with `Value`, `Type`, `Error`, `Success`. Also serves as the variable container. Error handling uses result checking (`data.Success`), not exceptions.

**Stream-based IO**: Output and input flow through named channels backed by streams.

**Entity events**: Goal, Step, and Action each have a `Lifecycle` with `Before`/`After` `Bindings`, plus pattern-matched event bindings via `EngineEvents`.

**Optional debugging**: CallStack is opt-in. When enabled, tracks frames with step history. Use `plang p !debug` to enable. Debug mode is owned by `engine.Debug` (EngineDebug).

## Handler Pattern

Handlers are small classes with `[Action("name")]`:

```csharp
[Action("set")]
public partial class Set : IContext
{
    [VariableName]
    public partial string Name { get; init; }
    public partial object? Value { get; init; }
    public partial string? Type { get; init; }

    public Task<Data> Run()
    {
        Context.MemoryStack.Set(Name, Value, Type != null ? Memory.Type.FromName(Type) : null);
        return Task.FromResult(Data.Ok(new types.variable { name = Name, value = Value, type = Type }));
    }
}
```

The source generator creates a `CodeGeneratedExecuteAsync` partial that resolves `%var%` references at property access time before calling `Run()`.

**Key**: Handlers don't implement any interface directly — just `[Action]` attribute, `IContext` for the Context property, and a `Run()` method. The source generator adds `ICodeGenerated` automatically.

## Memory System

- **MemoryStack**: `ConcurrentDictionary<string, Data>`, case-insensitive keys
- Supports **dot-notation paths**: `user.profile.email` navigates the object graph
- Supports **array indexing**: `items[0].name`, `items[idx].name` (variable in brackets)
- Special accessors: `.first`, `.last`, `.random`, `.count`
- System variables: `%Now%`, `%NowUtc%`, `%GUID%`

## Context System

- **PLangContext**: Per-request (MemoryStack, CallStack, Actor, current Goal/Step, EventScope for System/User events)
- **Actor**: Identity (System/Service/User), each owns a PLangContext and EngineChannels instance

## Builder Pipeline (.goal → .pr)

The builder transforms natural language PLang into JSON execution plans. It runs on the **old v1 engine** and produces Runtime2 artifacts:

```
Start.goal (natural language)
  → Build.goal (orchestrator, runs on v1 engine)
    → GetGoalsV2() parses .goal text → Runtime2 Goal objects
    → MergeV2PrData() loads existing .pr actions (incremental builds)
    → Renders goal+actions for LLM via Scriban template
    → LLM returns {module, action, parameters} for each step
    → ApplyStep validates & merges
    → SaveGoal writes .build/start.pr (JSON)
```

The bridge is `PLang/Modules/PlangModule/Program.cs` — exposes Runtime2 operations to the v1 builder engine.

### Build commands
- `plang build` — old v1 builder (used to build the builder goals in system/)
- `plang p build` — Runtime2 builder (builds user .goal files)

## Components

| Component | Description | Detail |
|-----------|-------------|--------|
| [Engine](engine.md) | Central orchestrator. Loads goals, manages handlers, executes via actors | Core |
| [Contexts](contexts.md) | `PLangContext` (request), `Actor` (identity) | Lifetime |
| [IO & Channels](io-channels.md) | Stream-based IO with named channels (EngineChannels) | `Channel` |
| [Goals & Steps](goals-steps.md) | `Goal`, `Step`, `Action` entities and smart collections (EngineGoals, GoalSteps, StepActions) | Execution structure |
| [Data](goal-result.md) | Universal value container AND result type | Return value + variable |
| [MemoryStack](memory-stack.md) | Variable storage with dot-notation, system variables | Variables |
| [CallStack](call-stack.md) | Execution tracking with frames, max depth 1000 | Debugging |
| [Events](events.md) | Lifecycle (Before/After Bindings) + EngineEvents with pattern matching | Lifecycle hooks |
| [Action Handlers](modules.md) | `[Action]` + `IContext` + `Run()`, source generator adds `ICodeGenerated`. `Library`, `EngineLibraries` (in Engine/) | Extensibility |
| [Serializers](serializers.md) | `ISerializer` with EngineSerializers, content-type routing | Data formats |
| [.pr File Format](pr-file-format.md) | JSON structure for compiled goals | File spec |
| [Errors](exceptions.md) | `IError`/`Error` hierarchy + `Runtime2Exception` | Error handling |
| [OBP Pattern](plang_object_based_pattern.md) | Object-Based Pattern with code examples | Design guide |

## File Structure

```
PLang/Runtime2/
├── Engine/
│   ├── Engine.cs              Central orchestrator (root of object graph)
│   ├── Goal.cs                Goal entity (properties)
│   ├── Goal.Methods.cs        Goal runtime methods (Load, RunAsync)
│   ├── EngineGoals.cs         Goal collection with lazy disk loading
│   ├── GoalCall.cs            Strongly-typed goal reference (name, parameters)
│   ├── Step.cs                Step entity (properties)
│   ├── Step.Methods.cs        Step runtime methods (Load, RunAsync)
│   ├── GoalSteps.cs           GoalSteps : List<Step> (smart collection)
│   ├── Action.cs              Action entity (properties)
│   ├── Action.Methods.cs      Action runtime methods (RunAsync)
│   ├── StepActions.cs         StepActions : List<Action> (smart collection)
│   ├── CallStack.cs           Execution tracking
│   ├── CallFrame.cs           Stack frame with ExecutionPhase enum
│   ├── ExecutedStep.cs        Record of an executed step
│   ├── SerializableCallStack.cs  Serializable call stack/frame DTOs
│   ├── EventType.cs           Event type enum (BeforeGoal, AfterStep, etc.)
│   ├── EventBinding.cs        Event handler binding with pattern matching
│   ├── EngineEvents.cs        EngineEvents — global event collection + dispatch
│   ├── Lifecycle.cs           Per-entity lifecycle (Before/After Bindings)
│   ├── Bindings.cs            Bindings — ordered event binding collection
│   ├── EngineDebug.cs         Debug mode controller (engine.Debug)
│   ├── EngineTesting.cs       Test runner (engine.Testing)
│   ├── Library.cs             Single library (one assembly's handlers)
│   ├── EngineLibraries.cs     Smart collection, walk-the-list resolution (engine.Libraries)
│   ├── EngineProperty.cs      Key-value store with GoalCall resolution
│   ├── ErrorHandler.cs        Step error configuration
│   ├── CacheSettings.cs       Step cache configuration
│   ├── StepCache.cs           Step-level cache wrapper
│   ├── StepCacheEntry.cs      Cache entry type
│   ├── MemoryStepCache.cs     In-memory ICache implementation
│   ├── ICache.cs              Cache interface
│   ├── IAction.cs             Action interface
│   └── Info.cs                Version/build info
│
├── Engine/Context/
│   ├── PLangContext.cs         Per-request state (MemoryStack, CallStack, events)
│   ├── Actor.cs               Identity (System/Service/User)
│   └── EventScope.cs          Event scope wrapper (owns EngineEvents)
│
├── Engine/Memory/
│   ├── Data.cs                Universal container + Type class
│   ├── MemoryStack.cs         Variable storage (ConcurrentDictionary)
│   ├── Properties.cs          Properties : IList<Data>
│   ├── IValueNavigator.cs     Navigation interface for dot-paths
│   ├── PlangTypeConverter.cs  Type conversion utilities
│   ├── TString.cs             Translatable string type
│   └── TypeJsonConverter.cs   JSON converter for Type
│
├── Engine/Errors/
│   ├── IError.cs              Error interface
│   ├── Error.cs               Base error implementation
│   ├── GoalError.cs           Goal-level errors
│   ├── StepError.cs           Step-level errors
│   ├── ActionError.cs         Action-level errors
│   ├── ServiceError.cs        External service errors
│   ├── ProgramError.cs        Program-level errors
│   ├── ValidationError.cs     Validation errors
│   ├── AssertionError.cs      Test assertion errors
│   ├── ErrorCategory.cs       Error categorization
│   └── Exceptions.cs          Runtime2Exception types
│
├── Engine/Channels/
│   ├── EngineChannels.cs      Channel manager (named I/O routing)
│   ├── Channel.cs             Stream-backed channel
│   ├── ChannelData.cs         Channel data wrapper
│   └── Serializers/
│       ├── EngineSerializers.cs   Content-type routing registry
│       ├── ISerializer.cs         Serializer interface
│       ├── JsonStreamSerializer.cs  System.Text.Json implementation
│       ├── TextStreamSerializer.cs  Plain text implementation
│       └── ViewPropertyFilter.cs  View-based property filtering
│
├── Engine/View.cs               [Store], [LlmBuilder], [Debug], [Default] attributes
│
├── Engine/Utility/
│   ├── TypeMapping.cs         PLang type names + MIME → CLR types + ConvertTo
│   └── AppData.cs             Application data utilities
│
├── Engine/Mapping/
│   └── GoalMapper.cs          Building.Model → Runtime2 conversion
│
├── Engine/Parsing/
│   └── PrParser.cs            .pr file parser
│
└── actions/
    ├── IClass.cs              Handler interface
    ├── IContext.cs             Context-aware handler interface
    ├── ICodeGenerated.cs       Source-generated execution interface
    ├── Attributes.cs          [Action], [Default], [VariableName] attributes
    ├── variable/              variable.set, variable.get, variable.clear, ...
    ├── file/                  file.save, file.read, file.copy, ...
    ├── output/                output.write
    ├── condition/             if handler
    ├── event/                 before/after goal/step/action handlers
    ├── goal/                  goal.call handler
    ├── loop/                  foreach handler
    ├── list/                  list operations
    ├── math/                  math operations
    ├── convert/               type conversion
    ├── assert/                test assertions
    ├── mock/                  test mocking
    ├── error/                 error handling
    └── library/               dynamic library loading
```
