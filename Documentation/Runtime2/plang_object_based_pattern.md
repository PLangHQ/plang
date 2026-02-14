# PLang Runtime2: Object-Based Pattern Implementation

This document describes how PLang Runtime2 applies the Object-Based Pattern. For the general, language-agnostic pattern definition, see [`object_pattern_formal.md`](object_pattern_formal.md).

## 1. Engine Object Graph

Engine is the root. Everything hangs off it as properties:

```
Engine
  .Goals            -- Goals: loaded goals, lookup by name
  .Actions          -- ActionRegistry: discovers and resolves module handlers
  .Serializers      -- SerializerRegistry: serialize/deserialize
  .FileSystem       -- IPLangFileSystem: sandboxed file operations
  .Channels         -- Channels: named channel routing (stdin, stdout, stderr, custom)
  .Events           -- Events: app-level event collection
  .Cache            -- ICache: pluggable step cache
  .System           -- Actor: system trust level (lazy)
  .Service          -- Actor: service trust level (lazy)
  .User             -- Actor: user trust level (lazy)
  .Path             -- string: always "/" (relative root)
  .AbsolutePath     -- string: OS path (e.g. C:\myapp)
  .Environment      -- string: "production", "development", etc.
  .Culture          -- CultureInfo: formatting for dates, numbers
  .IsDebugMode      -- bool
  .IsTestMode       -- bool
  .ShutdownToken    -- CancellationToken: for graceful shutdown
  [key]             -- object?: key-value store (Get<T>, Set<T>, GetOrCreate<T>)
```

Each property name tells you what the object *is*. `Goals` manages goals. `Actions` manages handlers. `FileSystem` manages file access. You look at the name and know exactly where to navigate.

> **Naming principle**: If a property name could reasonably describe two different things, it's too broad. A name like `IO` is ambiguous — does it mean file I/O or channel I/O? Name it what it actually is, and responsibilities become obvious.

Each property is an object that **does things**. Goals knows how to load from files. Channels knows how to route named streams. Actions knows how to discover and resolve handlers. They are not passive data bags.

Every handler that needs the engine gets a reference to it and navigates from there:

```csharp
// A handler reaches Channels and FileSystem through Engine
public sealed partial class WriteHandler : BaseClass<write>
{
    protected override async Task<Data> ExecuteAsync(write p)
    {
        var result = await Engine.Channels.WriteTextAsync(Runtime2.IO.Channels.StdOut, p.content?.ToString());
        if (!result.Success) return result;
        return Success(new types.output { content = p.content, channel = Runtime2.IO.Channels.StdOut });
    }
}
```

## 2. Entity Hierarchy

The core domain model follows this structure:

```
Goal
  .Name             -- string
  .Steps            -- Steps (collection wrapper)
  .Lifecycle        -- Lifecycle (Before/After bindings, not serialized)
  .Path / .PrPath   -- file system locations
  .Parent           -- Goal? (back-reference, not serialized)

Step
  .Index            -- int
  .Text             -- string (the PLang instruction)
  .Actions          -- Actions (collection wrapper)
  .Lifecycle        -- Lifecycle (Before/After bindings, not serialized)
  .Goal             -- Goal? (back-reference, not serialized)

Action
  .Class            -- string (module name, e.g. "variable")
  .Method           -- string (action name, e.g. "set")
  .Parameters       -- List<Data> (input parameters)
  .Return           -- List<Data>? (variable mappings for return values)
  .Lifecycle        -- Lifecycle (Before/After bindings, not serialized)
```

Navigation reads naturally:

```csharp
goal.Steps[0]                   // "goal's first step"
step.Actions[0].Class           // "step's first action's class"
goal.Steps[0].Actions[0]        // full chain from goal to action
```

## 3. Collections Own Their Loops

Collection wrappers inherit from `List<T>` and add domain operations. Parents delegate — they never iterate collections themselves.

### Steps

```csharp
public sealed class Steps : List<Step>
{
    public List<Step> Value => this;

    public async Task Load(PLangContext context)
    {
        foreach (var step in this)
            await step.Load(context);
    }
}
```

### Actions

```csharp
public sealed class Actions : List<Action>
{
    public List<Action> Value => this;

    public async Task Load(PLangContext context)
    {
        foreach (var action in this)
            await action.Load(context);
    }

    public async Task<Data> RunAsync(Engine engine, PLangContext context, CancellationToken ct = default)
    {
        Data merged = Data.Ok();
        foreach (var action in this)
        {
            var result = await action.RunAsync(engine, context, ct);
            if (!result.Success) return result;
            merged = merged.Merge(result);
        }
        return merged;
    }
}
```

### Parents delegate

```csharp
// Goal delegates to Steps
public async Task Load(PLangContext context)
{
    await Lifecycle.Before.Run(context);
    await Steps.Load(context);              // delegates, does not loop
    await Lifecycle.After.Run(context);
}

// Step delegates to Actions
public async Task Load(PLangContext context)
{
    await Lifecycle.Before.Run(context);
    await Actions.Load(context);            // delegates, does not loop
    await Lifecycle.After.Run(context);
}
```

## 4. Events: Entity-Owned Lifecycle

Events are owned by the entity they apply to. Each entity (Goal, Step, Action) has a `Lifecycle` property — the same type for all three.

### Structure

```csharp
// A collection of event bindings
public sealed class Bindings
{
    public int Count { get; }
    public void Add(EventBinding binding);
    public async Task<Data> Run(PLangContext context);
}

// The lifecycle of any entity
public sealed class Lifecycle
{
    public Bindings Before { get; }
    public Bindings After { get; }
}
```

Every entity gets the same structure. There is no special "load" property — "load" is a verb and has no place in a property name. Each binding knows its own EventType (e.g., `BeforeGoalLoad` vs `BeforeGoal`); the Lifecycle structure does not distinguish between them. It is just Before and After.

### Navigation reads naturally

```csharp
goal.Lifecycle.Before.Run(context)      // "goal's lifecycle, before — run the bindings"
goal.Lifecycle.After.Run(context)       // "goal's lifecycle, after — run the bindings"
step.Lifecycle.Before.Run(context)      // "step's lifecycle, before — run the bindings"
action.Lifecycle.Before.Run(context)    // "action's lifecycle, before — run the bindings"
```

### Execution flow

```
goal.Lifecycle.Before.Run(context)
  step.Lifecycle.Before.Run(context)
    action.Lifecycle.Before.Run(context)
    action.RunAsync(engine, context)         -- actual execution
    action.Lifecycle.After.Run(context)
  step.Lifecycle.After.Run(context)
goal.Lifecycle.After.Run(context)
```

## 5. Handlers Navigate Through Engine

Handlers extend `BaseClass<TParams>` and receive `Engine` and `Context` via `Initialize()`. They navigate to system capabilities through the engine.

### Channel navigation

Instead of `Console.WriteLine`, handlers write to named channels through `Engine.Channels`:

```csharp
// output.write handler
Engine.Channels.WriteTextAsync(IO.Channels.StdOut, text)

// Reading from a channel
Engine.Channels.ReadTextAsync(IO.Channels.StdIn)

// Writing structured data (serialized through engine.Serializers)
Engine.Channels.WriteAsync("stdout", data, "application/json")
```

Channels manages named streams (`stdin`, `stdout`, `stderr`, or custom). Each channel wraps a stream with direction and content type:

```csharp
engine.Channels.Get("stdout")                            // get a channel
engine.Channels.GetOrCreate("custom", () => channel)     // get or create
engine.Channels.CreateMemoryChannel("buffer")             // in-memory
engine.Channels.CreateFileChannel("log", path, FileMode.Append)  // file-backed
```

### Serializer navigation

Instead of `JsonSerializer.Serialize(obj)`, handlers use the engine's serializer registry:

```csharp
// Serialize to a stream (used internally by Channels.WriteAsync)
engine.Serializers.SerializeAsync(new SerializeOptions
{
    Stream = channel.Stream,
    Data = data,
    ContentType = "application/json"
});

// Deserialize from a file (used by Channels.ReadAsync)
engine.Serializers.Deserialize<T>(new DeserializeOptions { Value = content, Extension = ".json" });
```

### FileSystem navigation

```csharp
// Handler accesses FileSystem through Engine (also exposed as BaseClass.FileSystem)
Engine.FileSystem.File.ReadAllTextAsync(path)
Engine.FileSystem.File.WriteAllTextAsync(path, content)
Engine.FileSystem.Directory.Exists(path)
```

### Action resolution

```csharp
// Resolves a handler by module name and method name
var (handler, error) = engine.Actions.GetCodeGenerated("variable", "set", context);
```

### Goal navigation

Handlers can call other goals through `Engine.RunGoalAsync`. This is how control-flow actions like `condition.if` work — the handler evaluates a condition and delegates to a named goal:

```csharp
// condition.if handler delegates to a goal
var result = await Engine.RunGoalAsync(goalName, Context, CancellationToken);
```

The handler passes its own `Context`, so the called goal shares the same `MemoryStack`. This means variables set by earlier steps (e.g., `%fileResult.Exists%` from `file.exists`) are visible inside the called goal.

### Handler naming conventions

Records and handlers follow a consistent naming scheme:

| Element | Convention | Example |
|---------|-----------|---------|
| **Record** (parameters) | lowercase action name | `set`, `save`, `exists`, `@if` |
| **Handler** (execution) | PascalCase + `Handler` suffix, `partial` | `SetHandler`, `SaveHandler`, `IfHandler` |
| **Namespace** | `PLang.Runtime2.modules.{module}` | `modules.condition`, `modules.file` |
| **Registry key** | `{module}.{record}` | `condition.if`, `file.exists` |

When a record name collides with a C# keyword, prefix it with `@`:

```csharp
public record @if               // type name is "if" at runtime
{
    public virtual bool condition { get; init; }
}

public sealed partial class IfHandler : BaseClass<@if> { ... }
```

The source generator strips the `@` prefix, so the action registry key becomes `"if"` and PLang references it as `condition.if`. This pattern applies to any future keyword-named actions (`@switch`, `@for`, `@while`, etc.).

## 6. What You Can Access: Context and Engine

During execution every handler has two roots: `Engine` (system capabilities) and `Context` (request state). Together they determine what is reachable at any point.

### Engine (system-level, self-contained)

```
Engine
  .Goals              Goals — loaded goals, lookup by name
  .Actions            ActionRegistry — discover and resolve handlers
  .Serializers        SerializerRegistry — serialize / deserialize
  .FileSystem         IPLangFileSystem — sandboxed file I/O
  .Channels           Channels — named channel routing (stdin, stdout, stderr, custom)
  .Events             Events — app-level event collection
  .Cache              ICache — pluggable step cache
  .System / .Service / .User   Actor — trust-level identities (lazy)
  .Path               string — always "/" (relative root)
  .AbsolutePath       string — OS path (e.g. C:\myapp)
  .Environment        string — "production", "development", etc.
  .Culture            CultureInfo — formatting for dates, numbers
  .IsDebugMode        bool
  .IsTestMode         bool
  .ShutdownToken      CancellationToken — graceful shutdown
  .StartedAt          DateTime
  .Uptime             TimeSpan
  [key]               object? — key-value store (Get<T>, Set<T>, GetOrCreate<T>)
```

### PLangContext (request-level, per-execution)

```
Context
  .Id                 string — unique execution id
  .Engine             Engine — back-reference to the engine (non-nullable)
  .MemoryStack         MemoryStack — all %variables% for this execution
  .CallStack           CallStack? — goal/step call frames, stack trace, errors
  .Goal                Goal? — the goal currently executing
  .Step                Step? — the step currently executing
  .Actor               Actor? — the actor that owns this context
  .Parent              PLangContext? — parent context (for nested calls)
  .IsAsync             bool — whether this is an async execution
  .CancellationToken   CancellationToken — linked to Engine.ShutdownToken
  .CreatedAt           DateTime
  .Duration            TimeSpan
  .System              EventScope — system-level event bindings
  .User                EventScope — user-level event bindings
  .EventOverride       Data? — set by event.skipAction to override action results
  [key]                object? — arbitrary key-value store (Get<T>, Set<T>)
```

### MemoryStack (variables)

```
MemoryStack
  .Set(name, value, type?)     store a variable
  .Get(name)                   get by name — supports dot notation: "user.Name", "items[0].Value"
  .Get<T>(name)                get typed value
  .GetValue(name)              get raw object
  .Contains(name)              check existence
  .Remove(name)                remove
  .GetAll()                    all variables ordered by last update
  .Clone()                     shallow copy (for child contexts)
```

Built-in system variables (always present): `%Now%`, `%NowUtc%`, `%GUID%` (dynamic, computed on access).

### CallStack (execution trace)

```
CallStack
  .Current             CallFrame? — top frame
  .Depth               int
  .Push(goalName)      push new frame
  .Pop()               complete and pop
  .RecordStep(index, text)
  .AddError(error)
  .GetErrors()         all errors across frames
  .GetStackTrace()     formatted string
  .ContainsGoal(name)  recursion check
  .IsInEvent           bool — currently inside an event handler
```

### BaseClass convenience properties

Handlers extend `BaseClass<TParams>` which exposes shortcuts so you don't always navigate manually:

```csharp
protected MemoryStack MemoryStack => Context.MemoryStack;
protected CancellationToken CancellationToken => Context.CancellationToken;
protected IPLangFileSystem FileSystem => Engine.FileSystem;

// Variable helpers
protected Data? GetVariable(string name) => MemoryStack.Get(name);
protected T? GetVariable<T>(string name) => MemoryStack.Get<T>(name);
protected void SetVariable(string name, object? value, Type? type = null)
    => MemoryStack.Set(name, value, type);

// Result helpers
protected static Data Success() => Data.Ok();
protected static Data Success(object? value) => Data.Ok(value);
protected static Data Error(string message, ...) => Data.Fail(...);
```

### What can I access at this point?

Given `Engine` + `Context`, a handler can reach:

| Need | Navigation |
|------|-----------|
| Read/write a variable | `MemoryStack.Get("name")` / `MemoryStack.Set("name", value)` |
| Read a variable with dot path | `MemoryStack.Get("fileResult.Exists")` |
| Write to stdout | `Engine.Channels.WriteTextAsync(Channels.StdOut, text)` |
| Read/write files | `Engine.FileSystem.File.ReadAllTextAsync(path)` |
| Call another goal | `Engine.RunGoalAsync(goalName, Context, CancellationToken)` |
| Resolve a handler | `Engine.Actions.GetCodeGenerated("module", "method", Context)` |
| Serialize data | `Engine.Serializers.SerializeAsync(options)` |
| Check current goal/step | `Context.Goal`, `Context.Step` |
| Check call depth | `Context.Depth`, `Context.CallStack.Depth` |
| Detect recursion | `Context.CallStack.ContainsGoal("GoalName")` |
| Get app root path | `Engine.AbsolutePath` |
| Check environment | `Engine.Environment` |
| Store request-scoped data | `Context["key"] = value` / `Context.Get<T>("key")` |
| Store app-scoped data | `Engine["key"] = value` / `Engine.Get<T>("key")` |
| Cancel execution | `Context.Cancel()` |

## 7. Data Owns Its Composition

`Data` is the universal result type. It carries `Value`, `Error`, `Success`, and static helpers `Ok()` and `Fail()`. When multiple actions produce results, the merge logic lives on `Data` — it knows how to combine `List<Data>` by name:

```csharp
public class Data
{
    public object? Value { get; set; }
    public IError? Error { get; set; }
    public bool Success => Error == null;

    public static Data Ok() => new("");
    public static Data Ok(object? value) => new("", value);
    public static Data Fail(IError error) => new("") { Error = error };

    public Data Merge(Data other)
    {
        if (other.Value == null) return this;

        var myData = Value as List<Data> ?? new();
        var otherData = other.Value as List<Data> ?? new();

        foreach (var data in otherData)
        {
            var existing = myData.FindIndex(d =>
                string.Equals(d.Name, data.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
                myData[existing] = data;    // replace by name
            else
                myData.Add(data);           // append new
        }

        return new Data("") { Value = myData };
    }
}
```

The `Actions` collection uses `Merge` in its loop — it never inspects the data structure directly.

## 8. Do and Don't

### DO: Put behavior on the object that owns the data

```csharp
goal.Steps.Load(context);                      // Steps owns the step list
step.Actions.RunAsync(engine, context, ct);     // Actions owns the action list
engine.Goals.Get("Start");                      // Goals owns goal lookup
engine.Actions.GetCodeGenerated("file", "save", context);  // Actions owns handler resolution
```

### DON'T: Iterate someone else's collection

```csharp
// Wrong: this loop belongs on Steps
foreach (var step in goal.Steps)
    await step.Load(context);

// Wrong: this loop belongs on Actions
foreach (var action in step.Actions)
    await action.Load(context);

// Wrong: Goals owns goal loading
engine.LoadGoal(path);
```

### DO: Navigate to what you need through properties

```csharp
Engine.Channels.WriteTextAsync(IO.Channels.StdOut, data);
Engine.FileSystem.File.ReadAllTextAsync(path);
Engine.Serializers.SerializeAsync(options);
engine.Goals.Get("Start");
```

### DON'T: Pass everything through parameters

```csharp
// Wrong: passing IO, goals, events separately
async Task RunStep(Engine engine, IO io, Goals goals) { }

// Correct: reach them through engine
async Task RunStep(Engine engine)
{
    engine.Channels ...
    engine.Goals ...
}
```

### DO: Keep object references, not decomposed fields

```csharp
// StepError keeps a reference to the Step
public class StepError : Error
{
    public Step? Step { get; init; }

    public static StepError FromException(Exception ex, PLangContext context)
    {
        return new StepError(ex.Message, context)
        {
            Exception = ex,
            Step = context.Step    // keep the object, not step.Text
        };
    }
}
```

### DON'T: Create wrapper objects for data you already have

```csharp
// Wrong: EventContext duplicates what PLangContext already has
var result = await Events.Before.RunAsync(new EventContext
{
    GoalName = context.CurrentGoalName,
    StepIndex = context.CurrentStepIndex,
    StepText = Text
});

// Correct: PLangContext already has Goal, Step, CurrentGoalName, CurrentStepIndex
var result = await Events.Before.Run(context);
```

## 9. Context Rules

### Don't cache context on shared objects

Goal and Step objects may be shared between threads. Storing `PLangContext` on them creates race conditions.

```csharp
// Wrong: _context is shared, multiple threads overwrite it
public sealed partial class Goal
{
    internal PLangContext? _context;
    public Lifecycle Lifecycle => _context?.LifecycleFor(this) ?? _fallback;
}

// Correct: Lifecycle is a direct property, context passed as parameter
public sealed partial class Goal
{
    public Lifecycle Lifecycle { get; } = new();    // per-object, set at load time
}

await Lifecycle.Before.Run(context);   // context passed as parameter
```

**Rule**: If something is per-request (like which context is executing), pass it as a parameter. If something is per-object (like which event bindings apply), store it on the object.

### Back-references use `[JsonIgnore]`

```csharp
[JsonIgnore] public Goal? Parent { get; set; }  // Goal -> parent Goal
[JsonIgnore] public Goal? Goal { get; set; }     // Step -> owning Goal
[JsonIgnore] public Lifecycle Lifecycle { get; } // not serialized
```

These are set after deserialization (e.g., `step.Goal = goal` during goal loading) and excluded from serialization to avoid circular references.

## 10. Summary

1. **Engine is the root** — everything hangs off it: Goals, Actions, Serializers, FileSystem, Channels, Events, Actors, config, key-value store
2. **Names describe what the object is** — `Goals`, `Channels`, `Lifecycle`, `Bindings` — not vague verbs like `IO` or suffixes like `Manager`
3. **Structures are things** — a `Lifecycle` with Before/After IS a lifecycle; don't rename to `EventManager`
4. **Properties are nouns, methods are verbs** — never put a verb in a property name; `Lifecycle.Before`, not `Lifecycle.Load`
5. **Properties navigate** — `engine.Channels.WriteTextAsync(...)`, `engine.Goals.Get(...)`, `goal.Lifecycle.Before.Run(...)`
6. **Collections own their loops** — `Steps.Load()`, `Actions.RunAsync()`, not a foreach in Goal or Step
7. **Methods belong on the owner** — Goals loads goals, Steps loads steps, Data merges results
8. **Read like sentences** — `goal.Lifecycle.Before.Run(context)` reads naturally
9. **Events are entity-owned** — `Lifecycle` with `Bindings` (Before/After), same type for Goal, Step, Action
10. **Handlers navigate through Engine** — Channels, FileSystem, Serializers are all reachable from Engine
11. **No unnecessary DTOs** — use real types inside the runtime, DTOs only at serialization boundaries
12. **Don't cache request state on shared objects** — pass `PLangContext` as a parameter, don't store it
13. **Keep object references** — `StepError.Step`, not `StepError.StepText`
14. **Data owns merge** — `Data.Merge()` combines `List<Data>` by name
15. **Don't create wrapper objects** — if the data is on `PLangContext`, pass `PLangContext`
