# PLang Runtime Technical Reference

The Runtime is the core execution engine of the PLang programming language. It executes compiled PLang goals (`.pr` files).

## Architecture Overview

PLang is a natural language programming language. The Runtime loads and executes compiled goals. The Engine is minimal — it loads goals and runs them. `plang.exe` is conceptually just a goal: `- run plang app %data.path%, %data.parameters%, write to %app%` then `%app.wait%`.

### Design Principles

**Object-based architecture**: Modules expose a single entry point `ExecuteAsync(string method, object? parameters)` and receive typed request objects. The module implementation handles dispatch internally. This keeps the interface surface small and uniform across all modules.

**Stream-based IO**: Output and input flow through named channels. `IO` manages a collection of `Channel` objects, each backed by a `Stream`. Channels can be memory-backed, file-backed, or wrap any .NET stream.

**Universal return type**: `GoalResult` is the universal return type — wraps success/failure with `Success`, `Value`, and `Error` properties. Error handling uses result checking (`result.Success`), not exceptions for control flow.

**Extensible classes**: All core classes (`Engine`, `Goal`, `Step`) are `partial` — extensible by users in their own files.

**Optional debugging**: CallStack is opt-in per component. When enabled, it activates step tracking with frame history for debugging and audit.

## Component Diagram

```
Engine
├── AppContext       (PLangAppContext — app lifetime)
├── Modules          (ModuleRegistry — Execute-based modules)
├── Serializers      (SerializerRegistry — content-type based)
├── Goals            (Goal collection loaded from .pr files)
└── CreateContext()  → PLangContext
                        ├── MemoryStack   (variable storage)
                        ├── CallStack     (optional execution tracking)
                        └── IO            (named channels)
```

## Components

| Component | Description | Detail |
|-----------|-------------|--------|
| [Engine](engine.md) | Central execution engine. Loads goals, manages modules and serializers, executes goals and steps | Core orchestrator |
| [Contexts](contexts.md) | `PLangAppContext` (app lifetime) and `PLangContext` (per-request) manage state and configuration | Lifetime management |
| [IO & Channels](io-channels.md) | Stream-based IO with named channels for input/output operations | `IO`, `Channel`, `ChannelData` |
| [Goals & Steps](goals-steps.md) | `Goals` collection, `Goal` class, `Step` class — the execution units | Execution structure |
| [GoalResult](goal-result.md) | Universal return type with success/failure semantics | Return value pattern |
| [MemoryStack](memory-stack.md) | Variable storage with type metadata and system variables | Variable management |
| [CallStack](call-stack.md) | Optional execution tracking with `CallFrame` entries | Debugging support |
| [Events](events.md) | Event dispatching for before/after goal execution | Lifecycle hooks |
| [Modules](modules.md) | `IModule` interface, `BaseModule` base class, `ModuleRegistry` | Extensibility |
| [Serializers](serializers.md) | `ISerializer` interface with JSON and text implementations | Data format handling |
| [.pr File Format](pr-file-format.md) | JSON structure for compiled goals | File specification |
| [Exceptions](exceptions.md) | Custom exception types for runtime errors | Error handling |
| [Complete Example](complete-example.md) | End-to-end usage example | Full walkthrough |

## File Structure Reference

```
PLang/Runtime2/
├── Core/
│   ├── Engine.cs
│   ├── GoalResult.cs
│   ├── CallStack.cs
│   ├── CallFrame.cs
│   ├── EventCollection.cs
│   ├── Goal.cs
│   ├── Goals.cs
│   └── Step.cs
├── Context/
│   ├── PLangAppContext.cs
│   └── PLangContext.cs
├── Memory/
│   ├── MemoryStack.cs
│   ├── ObjectValue.cs
│   ├── Properties.cs
│   └── TypeInfo.cs
├── IO/
│   ├── IO.cs
│   ├── Channel.cs
│   └── ChannelData.cs
├── Serialization/
│   ├── ISerializer.cs
│   ├── JsonStreamSerializer.cs
│   ├── TextStreamSerializer.cs
│   └── SerializerRegistry.cs
├── Modules/
│   ├── IModule.cs
│   ├── BaseModule.cs
│   ├── ModuleRegistry.cs
│   └── VariableModule.cs
├── Errors/
│   ├── ErrorInfo.cs
│   └── Exceptions.cs
└── Utility/
    ├── TypeMapping.cs
    └── GoalData.cs
```
