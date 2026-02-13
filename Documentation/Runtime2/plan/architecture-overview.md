# Runtime2 Architecture Overview

> **Living document** — updated as the migration progresses and new features emerge.

---

## Engine (Root Object)

Engine is the single root of the entire object graph. Everything hangs off it.

```
Engine                              [PLang.Runtime2.Core.Engine]
├── Id: string                      unique engine instance id
├── Name: string                    "Runtime2"
├── RootPath: string                app root directory (from AppContext)
│
├── AppContext: PLangAppContext      ── app-lifetime shared state ──
│   ├── Id: string
│   ├── RootPath: string
│   ├── Environment: string         "production" | "development"
│   ├── Culture: CultureInfo        formatting locale (default: Invariant)
│   ├── IsDebugMode: bool
│   ├── IsTestMode: bool
│   ├── Events: Events              app-level event bindings
│   ├── Serializers: SerializerRegistry
│   ├── ShutdownToken               graceful shutdown
│   └── [key-value store]           arbitrary app-level data
│
├── FileSystem: IPLangFileSystem    ── all file I/O goes through this ──
│
├── Cache: ICache                   step-level cache (default: MemoryStepCache)
│
├── Actions: ActionRegistry         ── module/action handler lookup ──
│   ├── Register(module, action, IClass)      explicit instance (tests)
│   ├── RegisterCodeGenerated(module, action, Type)  per-call instantiation
│   ├── DiscoverAndRegister(assembly)          reflection scan
│   └── GetCodeGenerated(module, action) → ICodeGenerated
│
├── Serializers: SerializerRegistry ── content-type aware serialization ──
│   ├── Json: JsonStreamSerializer
│   ├── Text: TextStreamSerializer
│   └── GetOrDefault(contentType) → ISerializer
│
├── Goals: Goals                    ── loaded goal collection ──
│   ├── Add(goal), Get(name), GetAsync(name)
│   ├── GetByPrPathAsync(prPath)
│   ├── LoadFromFileAsync(prPath)
│   ├── LoadFromDirectoryAsync(dir)
│   └── Run(name, context)
│
├── IO: IO                          ── engine-level I/O router ──
│   ├── WriteAsync(actorName, channel, data)   routes to actor IO
│   ├── ReadAsync<T>(filePath)                 file deserialization
│   ├── Channels: default (stdout)
│   └── CreateMemoryChannel / CreateFileChannel
│
├── System: Actor                   ── lazy, for internal engine ops ──
├── Service: Actor                  ── lazy, for external services ──
└── User: Actor                     ── lazy, default execution actor ──
    ├── Context → PLangContext      convenience: Engine.Context = User.Context
    └── MemoryStack → ...           convenience: Engine.MemoryStack = User.Context.MemoryStack
```

---

## Actors

Three named actors, lazily created. Each owns its own context and IO channels.

```
Actor                               [PLang.Runtime2.Context.Actor]
├── Name: string                    "System" | "Service" | "User"
├── Engine: Engine                  back-reference to root
├── Context: PLangContext           per-actor execution context
└── IO: IO                          per-actor I/O channels
```

**Why three actors?**
- **User** — runs PLang app goals on behalf of the end user (default)
- **Service** — runs goals on behalf of external service calls (e.g., incoming HTTP)
- **System** — runs internal engine goals (e.g., `/system/error/` rendering)

Actors are resolved by name: `Engine.GetActor("user")`.
The builder knows valid values: `Actor.ValidValues = ["user", "service", "system"]`.

---

## Context (Per-Actor, Per-Request)

```
PLangContext                         [PLang.Runtime2.Context.PLangContext]
├── Id: string                       unique context instance
├── AppContext: PLangAppContext       shared app-level state
├── Actor: Actor?                    owning actor
├── Engine: Engine?                  set by RegisterContextVariables()
│
├── MemoryStack: MemoryStack         ── variable storage ──
│   ├── User variables               %name%, %items%, etc.
│   ├── System variables (!prefix)   !engine, !context, !memoryStack, !fileSystem,
│   │                                !callStack, !io, !serializers
│   ├── Dynamic system variables     !goal (current), !step (current)
│   └── Built-in dynamic vars        Now, NowUtc, GUID
│
├── CallStack: CallStack             ── tracks goal→step execution ──
│   ├── Push(goalName) → CallFrame
│   ├── Pop() → CallFrame
│   ├── Depth, MaxDepth (1000)
│   └── GetStackTrace()
│
├── Goal: Goal?                      currently executing goal
├── Step: Step?                      currently executing step
├── CurrentGoalName: string?
├── CurrentStepIndex: int?
│
├── System: EventScope               system-level events
│   └── Events: Events
├── User: EventScope                 user-level events
│   └── Events: Events
│
├── EventOverride: Data?             set by event.skipAction
├── Parent: PLangContext?            parent (for nested calls)
├── Depth: int                       nesting depth
│
├── EventsFor(Goal) → GoalStepEvents    cached event resolution
├── EventsFor(Step) → GoalStepEvents    cached event resolution
├── EventsFor(Action) → ActionEvents    cached event resolution
│
├── CreateChild(memoryStack?) → PLangContext
├── Clone(memoryStack?) → PLangContext
└── Cancel()
```

---

## Entity Hierarchy: Goal → Steps → Actions

This is the execution model. A `.pr` file deserializes into a Goal.

```
Goal                                 [PLang.Runtime2.Core.Goal]
├── Name: string                     "Start", "ProcessItem", etc.
├── Description: string?
├── Visibility: Public | Private
├── Path: string?                    relative path to .goal file
├── PrPath: string?                  derived: .build/<name>.pr
├── FolderPath: string               derived: /Cache/, /
├── Steps: Steps                     ── ordered step list ──
├── SubGoals: List<string>
├── InputParameters: Dict?
├── IsSetup / IsEvent / IsTest: bool
├── Parent: Goal?                    parent goal (sub-goals)
├── Engine: Engine?                  set on load
├── Errors / Warnings: List<Info>
│
├── Load(context) → Data            fires load events
├── RunAsync(engine, context) → Data runs all steps
└── ToText() → string               reconstructs .goal source

Steps : List<Step>                   [PLang.Runtime2.Core.Steps]
├── Load(context) → Data            loads each step
└── (inherits List<Step>)

Step                                 [PLang.Runtime2.Core.Step]
├── Index: int
├── Text: string                     "set %name% = 'hello'"
├── Indent: int
├── Comment: string?
├── Actions: Actions                 ── one or more actions per step ──
├── OnError: ErrorHandler?           error handling config
├── Cache: CacheSettings?            step-level cache config
├── StepCache: StepCache?            derived from Cache (lazy)
├── Timeout: int?
├── WaitForExecution: bool           default: true
├── Hash / PreviousHash: string?
├── Intent: string?
├── Goal: Goal?                      back-reference (JsonIgnore)
├── Errors / Warnings: List<Info>
│
├── Load(context) → Data
└── RunAsync(engine, context) → Data

Actions : List<Action>               [PLang.Runtime2.Core.Actions]
├── Load(context) → Data
├── RunAsync(engine, context) → Data  runs sequentially, merging results
└── Summary() → template rendering

Action                               [PLang.Runtime2.Core.Action]
├── Module: string                   "variable", "file", "output", etc.
├── ActionName: string               "set", "read", "write", etc.
├── Parameters: List<Data>           named parameters with types
├── Return: List<Data>?              return variable mappings
├── Errors / Warnings: List<Info>
├── Cacheable: bool
│
└── RunAsync(engine, context) → Data
    1. Resolve events: context.EventsFor(action)
    2. Fire BeforeAction events
    3. Get handler: engine.Actions.GetCodeGenerated(module, action)
    4. Initialize handler: handler.Initialize(engine, context)
    5. Resolve parameters via source-generated __Generated record
    6. handler.ExecuteAsync(resolvedParams) → Data
    7. Store return values in MemoryStack
    8. Fire AfterAction events
```

---

## Module System

An **action** is a `partial class` with `[Action("name")]`.
Properties are `partial` — the source generator provides their backing fields and
lazy `%var%` resolution. The action's business logic lives in `Run()`.
Optionally implements `IContext` when it needs access to Engine, MemoryStack, etc.

```
Example: variable/set.cs

    [Action("set", Cacheable = false)]
    public partial class Set : IContext
    {
        [VariableName]
        public partial string Name { get; init; }
        public partial object? Value { get; init; }
        public partial string? Type { get; init; }

        public Task<Data> Run()
        {
            Context.MemoryStack.Set(Name, Value, ...);
            return Task.FromResult(Data.Ok(...));
        }
    }
```

What the source generator adds (Set.Action.g.cs):
  - `ICodeGenerated` implementation
  - `Context` property (from IContext)
  - Backing fields + lazy getters that resolve %var% from MemoryStack
  - `CodeGeneratedExecuteAsync(params, engine, context)` → calls `Run()`
  - Parameter validation (non-nullable checks)
  - Exception wrapping → Data.FromError()


### Key Interfaces

```
IContext (optional)                    [PLang.Runtime2.modules.IContext]
└── Context: PLangContext             generated property, set before Run()
                                      only needed when action accesses Engine,
                                      MemoryStack, FileSystem, etc.

ICodeGenerated                       [PLang.Runtime2.modules.ICodeGenerated]
└── CodeGeneratedExecuteAsync(       generated entry point called by Engine
        params, engine, context)
        → Task<Data>

[Action("name")]                     [PLang.Runtime2.modules.ActionAttribute]
├── Name: string                     action name in .pr files
└── Cacheable: bool                  default: true

[VariableName]                       strips %% instead of resolving from memory
[Default(value)]                     default value when param not in .pr
```

### Action Attributes & Conventions

```
Action class anatomy:
  Namespace = PLang.Runtime2.modules.<module>/    → module name
  [Action("name")]                                → action name
  partial properties                              → parameters from .pr
  Run() → Task<Data>                              → business logic
  IContext (optional)                              → gets Context injected
                                                     only if action needs engine/memory/etc.

Types namespace:
  PLang.Runtime2.modules.<module>.types/          → return value records
  e.g. types.variable { name, value, type }
  e.g. types.@file { Path, Value, Size, Type }   → rich return objects
```

### Legacy Pattern (BaseClass<TParams>)

Still supported by the source generator but not the primary pattern:

```
BaseClass<TParams> : BaseClass : IClass
  - Uses separate record for parameters
  - Source generator creates record__Generated with lazy resolution
  - Handler's ExecuteAsync(TParams) receives the generated record
  - Initialize(engine, context) called before execution
```

### Source Generator Flow (Current [Action] Pattern)

1. Action class declares `partial` properties (Name, Value, Path, etc.)
2. Source generator emits backing fields + getters that resolve `%var%` from MemoryStack
3. Generator emits `CodeGeneratedExecuteAsync` that sets Context, validates params, calls `Run()`
4. At runtime: Engine calls `CodeGeneratedExecuteAsync` → properties resolve lazily → `Run()` executes

### Current Modules (13)

| Module | Actions | Status |
|--------|---------|--------|
| **variable** | set, get, exists, remove, clear | Done |
| **file** | read, save, copy, move, delete, exists, list | Done |
| **output** | write | Done |
| **condition** | if | Done |
| **goal** | call | Done |
| **event** | beforeGoal, afterGoal, beforeStep, afterStep, beforeAction, afterAction, skipAction, remove | Done |
| **loop** | foreach | Done |
| **error** | throw | Done |
| **list** | add, remove, get, set, count, contains, indexOf, join, split, first, last, unique, reverse, range, flatten, sort | Done |
| **math** | add, subtract, multiply, divide, modulo, power, sqrt, round, floor, ceiling, min, max, random, abs | Done |
| **convert** | toInt, toLong, toDouble, toBool, toDateTime, toString, toJson, fromJson, toBase64, fromBase64 | Done |
| **assert** | equals, notEquals, isTrue, isFalse, isNull, isNotNull, contains, greaterThan, lessThan | Done |
| **mock** | action (intercept), verify, reset | Done |

---

## I/O System

```
IO                                   [PLang.Runtime2.IO.IO]
├── Engine-level IO                  router — delegates to actor IO
│   ├── WriteAsync(actorName, channel, data)
│   └── ReadAsync<T>(filePath)       file + deserialize
│
├── Actor-level IO                   per-actor channels
│   ├── WriteAsync(channel, data)
│   ├── ReadTextAsync(channel)
│   └── Channels                     named channel registry
│
└── Channel                          [PLang.Runtime2.IO.Channel]
    ├── Name: string
    ├── Stream: Stream               backing stream
    ├── Direction: Input | Output | Bidirectional
    ├── ContentType: string?         drives serializer selection
    ├── IsOpen: bool
    │
    ├── Memory(name) → MemoryStream-backed
    ├── File(name, path) → FileStream-backed
    ├── Input(name, stream) / Output(name, stream)
    │
    ├── ReadAllTextAsync / ReadAllBytesAsync
    └── WriteTextAsync / WriteBytesAsync

Write flow:
  output.write("hello")
  → handler gets Engine.IO
  → IO.WriteAsync("default", "hello")
  → resolves Channel("default", stdout)
  → serializer.SerializeAsync(channel.Stream, data, contentType)
  → text appears on console
```

---

## Event System

Events fire at every level of execution. Two scopes per context.

```
Context.System.Events                system-level (engine internals)
Context.User.Events                  user-level (PLang code)

EventType enum:
  BeforeAppStart, AfterAppStart
  OnBeforeGoalLoad, OnAfterGoalLoad
  BeforeGoal, AfterGoal
  OnBeforeStepLoad, OnAfterStepLoad
  BeforeStep, AfterStep
  BeforeAction, AfterAction
  OnError, OnVariableChange
  OnCacheHit, OnCacheMiss

EventBinding:
  ├── Type: EventType
  ├── Handler: Func<PLangContext, Task<Data>>
  ├── GoalNamePattern: string?       glob/regex matching
  ├── StepPattern: string?
  ├── ActionPattern: string?         "variable.set", "file.*"
  ├── Priority: int                  higher runs first
  ├── StopOnError: bool
  └── Re-entry guard                 TryEnterEvent/ExitEvent on context

Event resolution (cached per context):
  context.EventsFor(Goal)   → GoalStepEvents { Load.Before, Load.After, Before, After }
  context.EventsFor(Step)   → GoalStepEvents { Load.Before, Load.After, Before, After }
  context.EventsFor(Action) → ActionEvents { Before, After }

EventOverride:
  event.skipAction handler sets context.EventOverride = Data
  → BeforeAction event returns the override instead of running the real handler
  → Used by mock module to intercept actions
```

---

## Memory System

```
MemoryStack                          [PLang.Runtime2.Memory.MemoryStack]
├── Variables: ConcurrentDictionary<string, Data>
│
├── Built-in dynamic vars:
│   ├── Now → DateTime.Now
│   ├── NowUtc → DateTime.UtcNow
│   └── GUID → Guid.NewGuid()
│
├── Context vars (! prefix, set by RegisterContextVariables):
│   ├── !engine, !context, !memoryStack
│   ├── !fileSystem, !callStack, !io, !serializers
│   └── !goal (dynamic), !step (dynamic)
│
├── Set(name, value, type?)
├── Get(name) → Data?               supports dot navigation, bracket indices
├── Get<T>(name) → T?
├── Contains(name), Remove(name)
├── Clone() → deep clone
└── Clear() (preserves system vars)

Data                                 [PLang.Runtime2.Memory.Data]
├── Name: string
├── Value: object?                   the actual value
├── Type: Type?                      PLang type (string, int, application/json, etc.)
├── Properties: Dict?                metadata
├── Error: IError?
├── Success: bool
├── Handled: bool                    for event override flow
│
├── Ok(value?) → Data               success factory
├── FromError(error) → Data          error factory
├── Merge(other) → Data             combine results
├── GetValue<T>() → T               typed extraction with conversion
├── GetChild(path) → Data?          dot-path navigation via ValueNavigators
│
└── Value setter unwraps JsonElement → Dictionary automatically

Type                                 [PLang.Runtime2.Memory.Type]
├── Value: string                    "string", "int", "application/json", etc.
├── ClrType: System.Type             derived via TypeMapping
└── Static instances: String, Int, Bool, DateTime, List, Dictionary, etc.

TString                              [PLang.Runtime2.Memory.TString]
├── Template: string                 "Hello %name%"
├── Resolver: Func<string, object?>  backed by MemoryStack at runtime
└── Resolve() → string              replaces %var% with resolved values
```

---

## Execution Flow

```
1. Engine.RunGoalAsync("Start")
   └── context = User.Context (default)

2. Goals.Run("Start", context)
   ├── Goals.GetAsync("Start")       loads .pr file if needed
   └── goal.RunAsync(engine, context)

3. Goal.RunAsync(engine, context)
   ├── CallStack.Push("Start")
   ├── Fire BeforeGoal events
   ├── Steps.RunAsync(engine, context)
   │   └── for each step:
   │       Step.RunAsync(engine, context)
   │       ├── Fire BeforeStep events
   │       ├── Check StepCache → if hit, skip execution
   │       ├── Actions.RunAsync(engine, context)
   │       │   └── for each action:
   │       │       Action.RunAsync(engine, context)
   │       │       ├── Fire BeforeAction events
   │       │       │   └── if EventOverride set → return override (mock path)
   │       │       ├── ActionRegistry.GetCodeGenerated(module, action)
   │       │       ├── action.CodeGeneratedExecuteAsync(params, engine, context)
   │       │       │   ├── Set Context (IContext)
   │       │       │   ├── Properties resolve %var% lazily from MemoryStack
   │       │       │   ├── Validate required params
   │       │       │   └── action.Run() → Data
   │       │       ├── Store Return values in MemoryStack
   │       │       └── Fire AfterAction events
   │       ├── Store result in StepCache (if configured)
   │       ├── Handle errors (OnError, retry)
   │       └── Fire AfterStep events
   ├── Fire AfterGoal events
   └── CallStack.Pop()

4. Return Data to caller
```

---

## Serialization

```
SerializerRegistry
├── JsonStreamSerializer             application/json, .json, .pr
├── TextStreamSerializer             text/plain, .txt
│   └── jsonFallback                 for text that looks like JSON
└── (future: yaml, xml, csv)

Used by:
- IO.ReadAsync<T>(file)              deserializes .pr files
- IO.WriteAsync(channel, data)       serializes output to channels
- Data.Value setter                  unwraps JsonElement on assignment
```

---

## Error Hierarchy

```
IError                               [PLang.Runtime2.Errors.IError]
├── Message, Key, StatusCode

Error : IError                       base implementation
├── Format() → category-aware display
├── ErrorCategory: Application (4xx) | Runtime (5xx)
└── FromException(ex)

ActionError : Error                  action-level (module.action context)
StepError : Error                    step-level
GoalError : Error                    goal-level
ServiceError : Error                 infrastructure-level
ProgramError : Error                 fatal program error
ValidationError : Error              input validation
AssertionError : Error               test assertions

Data carries errors:
  Data.FromError(new ActionError(...))
  Data.Success = false when Error != null
```

---

## File Layout

```
PLang/Runtime2/
├── Core/                    Engine, Goal, Step, Action, Steps, Actions, Goals
│   ├── CallStack.cs         execution tracking
│   ├── EventCollection.cs   Events class + EventBinding + EventType
│   ├── EventList.cs         GoalStepEvents, ActionEvents, BeforeAfterEvents
│   ├── GoalCall.cs          strongly-typed goal reference
│   ├── StepCache.cs         step result caching
│   └── ErrorHandler.cs      step-level error handling config
│
├── Context/                 PLangAppContext, PLangContext, Actor, EventScope
│
├── Memory/                  MemoryStack, Data, Type, TString, DynamicData
│   └── Navigators/          ValueNavigators for dot-path resolution
│
├── IO/                      IO, Channel, ChannelData
│
├── Errors/                  IError, Error, ActionError, StepError, etc.
│
├── Serialization/           SerializerRegistry, JsonStreamSerializer, TextStreamSerializer
│
├── Mapping/                 GoalMapper (v1→v2 bridge, removed in Phase 7)
│
├── Utility/                 TypeMapping, AppData
│
└── modules/                 action handlers
    ├── BaseClass.cs         base handler class
    ├── IClass.cs            handler interface
    ├── ICodeGenerated.cs    source generator marker
    ├── ActionRegistry.cs    module.action → handler lookup
    ├── ActionAttribute.cs   [Action] attribute
    ├── variable/            set, get, exists, remove, clear
    ├── file/                read, save, copy, move, delete, exists, list
    ├── output/              write
    ├── condition/           if
    ├── goal/                call
    ├── event/               before/after goal/step/action, skipAction, remove
    ├── loop/                foreach
    ├── error/               throw
    ├── list/                add, remove, get, set, count, contains, indexOf, ...
    ├── math/                add, subtract, multiply, divide, round, floor, ...
    ├── convert/             toInt, toDouble, toBool, toJson, fromJson, ...
    ├── assert/              equals, notEquals, isTrue, isFalse, ...
    └── mock/                action (intercept), verify, reset
```
