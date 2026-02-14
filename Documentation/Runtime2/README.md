# PLang Runtime2 — Architecture Overview

Runtime2 is PLang's second-generation execution engine. It replaces the v1 module system with an object-based action handler architecture, a universal `Data` result type, and source-generated lazy parameter resolution.

## The Object Graph

Engine is the root — everything hangs off it:

```
Engine (sealed, IAsyncDisposable)
├── Libraries        (Libraries — built-in [0] + external DLLs, handler resolution)
├── Serializers      (SerializerRegistry — content-type based)
├── Goals            (Goal collection with lazy disk loading)
├── FileSystem       (IPLangFileSystem — abstracted filesystem)
├── Channels         (Channel-based I/O routing)
├── Events           (Global event collection)
├── Cache            (ICache — pluggable step cache)
└── Actors (lazy)
    ├── System       (internal engine operations)
    ├── Service      (external service operations)
    └── User         (end user operations)
         └── Context → PLangContext
                        ├── MemoryStack   (variable storage)
                        ├── CallStack     (execution tracking)
                        ├── System/User   (EventScope)
                        └── Actor         (identity)
```

## Entity Hierarchy: Goal → Steps → Actions

```
Goal (one .pr file)
 └── Steps : List<Step>  (smart collection, owns RunAsync)
      └── Actions : List<Action>  (smart collection, owns RunAsync)
           └── Action → resolves to a Handler (e.g. variable/set → SetHandler)
```

Each level calls `.Load()` then `.RunAsync()`. Events fire before/after each phase.

## Execution Flow

```
plang p Start.goal
  → Engine loads .build/start.pr (JSON → Goal)
  → Engine.RunGoalAsync(goal, context)
    → Steps.RunAsync() → for each Step:
      → Actions.RunAsync() → for each Action:
        → Libraries.GetCodeGenerated(module, action) finds handler
        → Source-generated code resolves %variables% in params
        → handler.Run() executes, returns Data
        → Return values stored in MemoryStack
```

## Design Principles

**Object-Based Pattern (OBP)**:
1. **Behavior on owner** — `Steps.RunAsync()` iterates steps, not external code
2. **Navigate, don't pass** — pass Engine/Context, let caller reach what it needs
3. **Keep object references** — store `Step`, not `step.Text`; store `Goal`, not `goal.Name`
4. **Per-request state is a parameter** — PLangContext never cached on shared objects (Goal, Step)
5. **Smart collections** — Steps, Actions extend `List<T>` and own domain operations (Load, RunAsync)

**Strongly typed**: TypeMapping maps PLang type names to CLR types. Never weaken to `object` without explicit reason.

**Universal result type**: `Data` wraps success/failure with `Value`, `Type`, `Error`, `Success`. Also serves as the variable container. Error handling uses result checking (`data.Success`), not exceptions.

**Stream-based IO**: Output and input flow through named channels backed by streams.

**Entity events**: Goal, Step, and Action each have Before/After × Load/Run phases, plus pattern-matched event bindings.

**Optional debugging**: CallStack is opt-in. When enabled, tracks frames with step history. Use `plang p !debug` to enable.

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

**Key**: All handlers must implement `ICodeGenerated` — Engine has no fallback path.

## Memory System

- **MemoryStack**: `ConcurrentDictionary<string, Data>`, case-insensitive keys
- Supports **dot-notation paths**: `user.profile.email` navigates the object graph
- Supports **array indexing**: `items[0].name`, `items[idx].name` (variable in brackets)
- Special accessors: `.first`, `.last`, `.random`, `.count`
- System variables: `%Now%`, `%NowUtc%`, `%GUID%`

## Context System

- **PLangAppContext**: App-lifetime shared state (RootPath, Serializers, Logger)
- **PLangContext**: Per-request (MemoryStack, CallStack, Actor, current Goal/Step)
- **Actor**: Identity (System/Service/User), each owns a PLangContext and IO instance

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
| [Contexts](contexts.md) | `PLangAppContext` (app), `PLangContext` (request), `Actor` (identity) | Lifetime |
| [IO & Channels](io-channels.md) | Stream-based IO with named channels | `IO`, `Channel` |
| [Goals & Steps](goals-steps.md) | `Goal`, `Step`, `Action` entities and collection wrappers | Execution structure |
| [Data](goal-result.md) | Universal value container AND result type | Return value + variable |
| [MemoryStack](memory-stack.md) | Variable storage with dot-notation, system variables | Variables |
| [CallStack](call-stack.md) | Execution tracking with frames, max depth 1000 | Debugging |
| [Events](events.md) | Entity events + global Events with pattern matching | Lifecycle hooks |
| [Action Handlers](modules.md) | `IClass`, `IContext`, `ICodeGenerated`, `Library`, `Libraries` | Extensibility |
| [Serializers](serializers.md) | `ISerializer` with registry, content-type routing | Data formats |
| [.pr File Format](pr-file-format.md) | JSON structure for compiled goals | File spec |
| [Errors](exceptions.md) | `IError`/`Error` hierarchy + `Runtime2Exception` | Error handling |
| [OBP Pattern](plang_object_based_pattern.md) | Object-Based Pattern with code examples | Design guide |

## File Structure

```
PLang/Runtime2/
├── Core/
│   ├── Engine.cs            Central orchestrator
│   ├── Goal.cs              Goal entity (properties)
│   ├── GoalMethods.cs       Goal runtime methods (Load, RunAsync)
│   ├── Goals.cs             Goal collection with lazy disk loading
│   ├── GoalCall.cs          Strongly-typed goal reference (name, parameters)
│   ├── Step.cs              Step entity (properties)
│   ├── StepMethods.cs       Step runtime methods (Load, RunAsync)
│   ├── Action.cs            Action entity (properties)
│   ├── ActionMethods.cs     Action runtime methods (Load, RunAsync)
│   ├── Actions.cs           Actions : List<Action> with RunAsync
│   ├── Steps.cs             Steps : List<Step> with RunAsync
│   ├── CallStack.cs         Execution tracking
│   ├── CallFrame.cs         Stack frame with ExecutionPhase
│   ├── EventList.cs         EventList, PhaseEvents, EntityEvents
│   ├── EventCollection.cs   Events, EventBinding, EventType
│   ├── ErrorHandler.cs      Step error configuration
│   └── CacheSettings.cs     Step cache configuration
├── Context/
│   ├── PLangAppContext.cs    App-lifetime state
│   ├── PLangContext.cs       Per-request state
│   ├── Actor.cs             Identity (System/Service/User)
│   └── EventScope.cs        Event scope wrapper
├── Memory/
│   ├── Data.cs              Universal container + Type class
│   ├── MemoryStack.cs       Variable storage (ConcurrentDictionary)
│   └── Properties.cs        Properties : IList<Data>
├── Errors/
│   ├── IError.cs, Error.cs  Error hierarchy
│   ├── GoalError.cs, StepError.cs, ActionError.cs, ServiceError.cs
│   └── Exceptions.cs        Runtime2Exception types
├── modules/
│   ├── IClass.cs            Handler interface
│   ├── ICodeGenerated.cs    Source-generated execution interface
│   ├── BaseClass.cs         Abstract base + BaseClass<TParams>
│   ├── Library.cs           Single library (one assembly's handlers)
│   ├── Libraries.cs         Smart collection, walk-the-list resolution
│   ├── variable/            variable.set, variable.get, ...
│   ├── file/                file.save, file.read, file.copy, ...
│   ├── output/              output.write
│   ├── condition/           if handler
│   ├── event/               before/after goal/step/action handlers
│   └── goal/                goal.call handler
├── IO/
│   ├── IO.cs                Channel manager + file ReadAsync<T>
│   └── Channel.cs           Stream-backed channel
├── Serialization/
│   ├── ISerializer.cs       Serializer interface
│   ├── JsonStreamSerializer.cs  System.Text.Json implementation
│   ├── SerializerRegistry.cs    Content-type routing
│   └── View.cs              [Store], [LlmBuilder], [Debug], [Default] attributes
├── Utility/
│   └── TypeMapping.cs       PLang type names + MIME → CLR types + ConvertTo
└── Mapping/
    └── GoalMapper.cs        Building.Model → Runtime2.Core conversion
```
