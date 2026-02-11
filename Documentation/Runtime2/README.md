# PLang Runtime2 — Architecture Overview

Runtime2 is PLang's second-generation execution engine. It replaces the v1 module system with an object-based action handler architecture, a universal `Data` result type, and source-generated lazy parameter resolution.

## Architecture Overview

PLang is a natural language programming language. The Runtime loads and executes compiled goals. The Engine is minimal — it loads goals and runs them. `plang.exe` is conceptually just a goal: `- run plang app %data.path%, %data.parameters%, write to %app%` then `%app.wait%`.

### Design Principles

**Object-based architecture**: Action handlers expose typed parameter records and a `CodeGeneratedExecuteAsync` entry point. A source generator creates `ICodeGenerated` implementations that resolve `%var%` references lazily at property access time. This keeps the handler surface uniform across all actions.

**Stream-based IO**: Output and input flow through named channels. `IO` manages a collection of `Channel` objects, each backed by a `Stream`. Channels can be memory-backed, file-backed, or wrap any .NET stream.

**Universal result type**: `Data` is the universal return type — wraps success/failure with `Success`, `Value`, and `Error` properties. Also serves as the variable container (replaces the old `ObjectValue`). Error handling uses result checking (`result.Success`), not exceptions for control flow.

**Actor system**: Three trust levels (User, Service, System), each `Actor` owns a `PLangContext` and `IO` instance.

**Entity events**: Goal, Step, and Action each have `EntityEvents` with Before/After × Load/Run phases, plus a global `Events` system with 14 event types and pattern matching.

**Optional debugging**: CallStack is opt-in per component. When enabled, it activates step tracking with frame history for debugging and audit.

## Component Diagram

```
Engine (sealed, IAsyncDisposable)
├── AppContext       (PLangAppContext — app lifetime)
├── Actions          (ActionRegistry — namespace → class → IClass handler)
├── Serializers      (SerializerRegistry — content-type based)
├── Goals            (Goal collection with lazy disk loading)
├── FileSystem       (IPLangFileSystem — abstracted filesystem)
├── IO               (Channel-based I/O manager)
└── Actors (lazy)
    ├── System       (TrustLevel.System = 3)
    ├── Service      (TrustLevel.Service = 2)
    └── User         (TrustLevel.User = 1)
         └── Context → PLangContext
                        ├── MemoryStack   (variable storage)
                        ├── CallStack     (execution tracking)
                        ├── System/User   (EventScope)
                        └── Actor         (identity)
```

## Components

| Component | Description | Detail |
|-----------|-------------|--------|
| [Engine](engine.md) | Central orchestrator. Loads goals, manages action handlers and serializers, executes goals via actors | Core orchestrator |
| [Contexts](contexts.md) | `PLangAppContext` (app lifetime), `PLangContext` (per-request), `Actor` (identity with trust level), `EventScope` | Lifetime management |
| [IO & Channels](io-channels.md) | Stream-based IO with named channels for input/output operations | `IO`, `Channel` |
| [Goals & Steps](goals-steps.md) | `Goal`, `Step`, `Action` entities and their collection wrappers (`Goals`, `Steps`, `Actions`) | Execution structure |
| [Data](goal-result.md) | Universal value container AND result type — replaces both `GoalResult` and `ObjectValue` | Return value + variable pattern |
| [MemoryStack](memory-stack.md) | Variable storage with `Data` entries, dot-notation path resolution, system variables | Variable management |
| [CallStack](call-stack.md) | Execution tracking with `CallFrame` entries, max depth 1000 | Debugging support |
| [Events](events.md) | Entity events (Before/After × Load/Run) + global `Events` with 14 event types and pattern matching | Lifecycle hooks |
| [Action Handlers](modules.md) | `IClass` interface, `BaseClass` base, `ICodeGenerated`, `ActionRegistry` | Extensibility |
| [Serializers](serializers.md) | `ISerializer` interface with `SerializerRegistry`, content-type routing | Data format handling |
| [.pr File Format](pr-file-format.md) | JSON structure for compiled goals (v0.1 `.pr` and v0.2 `.pr.json`) | File specification |
| [Errors & Exceptions](exceptions.md) | `IError` / `Error` hierarchy + `Runtime2Exception` types | Error handling |
| [Complete Example](complete-example.md) | End-to-end usage example | Full walkthrough |

## File Structure Reference

```
PLang/Runtime2/
├── Core/
│   ├── Engine.cs            Central orchestrator
│   ├── Goal.cs              Goal entity (properties)
│   ├── GoalMethods.cs       Goal runtime methods (Load, RunAsync, FormatForLlm)
│   ├── Goals.cs             Goal collection with lazy disk loading
│   ├── Step.cs              Step entity (properties)
│   ├── StepMethods.cs       Step runtime methods (Load, RunAsync)
│   ├── Action.cs            Action entity (properties)
│   ├── ActionMethods.cs     Action runtime methods (Load, RunAsync)
│   ├── Actions.cs           Actions : List<Action> with RunAsync
│   ├── Steps.cs             Steps : List<Step>
│   ├── CallStack.cs         Execution tracking
│   ├── CallFrame.cs         Stack frame with ExecutionPhase
│   ├── EventList.cs         EventList, PhaseEvents, EntityEvents
│   ├── EventCollection.cs   Events, EventBinding, EventType (14 types)
│   ├── ErrorHandler.cs      Step error configuration
│   ├── Info.cs              Info { Key, Message }
│   └── CacheSettings.cs     Step cache configuration
├── Context/
│   ├── PLangAppContext.cs    App-lifetime state
│   ├── PLangContext.cs       Per-request state
│   ├── Actor.cs             Identity with TrustLevel
│   └── EventScope.cs        Event scope wrapper
├── Memory/
│   ├── Data.cs              Universal value container + Type class + Data<T> + DynamicData
│   ├── MemoryStack.cs       Variable storage (ConcurrentDictionary<string, Data>)
│   └── Properties.cs        Properties : IList<Data>
├── Errors/
│   ├── IError.cs            Error interface
│   ├── Error.cs             Base error class
│   ├── GoalError.cs         Goal-specific errors
│   ├── StepError.cs         Step-specific errors
│   ├── ActionError.cs       Action-specific errors
│   ├── ServiceError.cs      Handler internal errors
│   └── Exceptions.cs        Runtime2Exception hierarchy
├── actions/
│   ├── IClass.cs            Handler interface
│   ├── ICodeGenerated.cs    Source-generated execution interface
│   ├── BaseClass.cs         Abstract base + BaseClass<TParams>
│   ├── ActionRegistry.cs    Two-level handler lookup
│   ├── variable/            variable.set, variable.get, variable.remove, ...
│   ├── file/                file.save, file.read, file.copy, file.delete, ...
│   ├── output/              output.write
│   └── condition/           condition handlers
├── IO/
│   ├── IO.cs                Channel manager + file ReadAsync<T>
│   └── Channel.cs           Stream-backed channel
├── Serialization/
│   ├── ISerializer.cs       Serializer interface
│   └── SerializerRegistry.cs Content-type routing
├── Utility/
│   └── TypeMapping.cs       PLang types + MIME → CLR types
├── Mapping/
│   └── GoalMapper.cs        Building.Model → Runtime2.Core conversion
└── Parsing/
    └── PrParser.cs          v0.1 .pr and v0.2 .pr.json parser
```
