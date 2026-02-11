# PLang Runtime2: Object-Based Pattern Implementation

This document describes how PLang Runtime2 applies the Object-Based Pattern. For the general, language-agnostic pattern definition, see [`object_pattern_formal.md`](object_pattern_formal.md).

## 1. Engine Object Graph

Engine is the root. Everything hangs off it as properties:

```
Engine
  .AppContext       -- PLangAppContext: app-level config, global events, serializers
  .Actions          -- ActionRegistry: discovers and resolves module handlers
  .Serializers      -- SerializerRegistry: serialize/deserialize through engine
  .Goals            -- Goals: ConcurrentDictionary of loaded goals
  .FileSystem       -- IPLangFileSystem: sandboxed file operations
  .IO               -- IO: channel-based I/O (stdin, stdout, stderr)
  .System           -- Actor: system trust level (lazy)
  .Service          -- Actor: service trust level (lazy)
  .User             -- Actor: user trust level (lazy)
  .IsDebugMode      -- bool: delegates to AppContext
```

Each property is an object that **does things**. Goals knows how to load from files. IO knows how to read/write channels. Actions knows how to discover and resolve handlers. They are not passive data bags.

Every handler that needs the engine gets a reference to it and navigates from there:

```csharp
// A handler reaches IO and FileSystem through Engine
public sealed partial class WriteHandler : BaseClass<write>
{
    protected override async Task<Data> ExecuteAsync(write p)
    {
        var result = await Engine.IO.WriteTextAsync(Runtime2.IO.IO.StdOut, p.content?.ToString());
        if (!result.Success) return result;
        return Success(new types.output { content = p.content, channel = Runtime2.IO.IO.StdOut });
    }
}
```

## 2. Entity Hierarchy

The core domain model follows this structure:

```
Goal
  .Name             -- string
  .Steps            -- Steps (collection wrapper)
  .Events           -- EntityEvents (entity-owned, not serialized)
  .Path / .PrPath   -- file system locations
  .Parent           -- Goal? (back-reference, not serialized)

Step
  .Index            -- int
  .Text             -- string (the PLang instruction)
  .Actions          -- Actions (collection wrapper)
  .Events           -- EntityEvents (entity-owned, not serialized)
  .Goal             -- Goal? (back-reference, not serialized)

Action
  .Class            -- string (module name, e.g. "variable")
  .Method           -- string (action name, e.g. "set")
  .Parameters       -- List<Data> (input parameters)
  .Return           -- List<Data>? (variable mappings for return values)
  .Events           -- EntityEvents (entity-owned, not serialized)
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
// Goal.Load() -- delegates to Steps
public async Task Load(PLangContext context)
{
    context.PopulateLoadEvents(Events, EventType.OnBeforeGoalLoad, EventType.OnAfterGoalLoad);
    await Events.Before.Load.Run(context);
    await Steps.Load(context);          // delegates, does not loop
    await Events.After.Load.Run(context);
}

// Step.Load() -- delegates to Actions
public async Task Load(PLangContext context)
{
    context.PopulateLoadEvents(Events, EventType.OnBeforeStepLoad, EventType.OnAfterStepLoad);
    await Events.Before.Load.Run(context);
    await Actions.Load(context);         // delegates, does not loop
    await Events.After.Load.Run(context);
}
```

## 4. Events: Entity-Owned, Phase Navigation

Events are owned by the entity they apply to, not by a central event manager. Each entity (Goal, Step, Action) has an `Events` property of type `EntityEvents`.

### Event type hierarchy

```csharp
// A flat list of event bindings that can be Run
public sealed class EventList
{
    public int Count { get; }
    public void Add(EventBinding binding);
    public async Task<Data> Run(PLangContext context);
}

// Two phases: Load-time events and Runtime events
public sealed class PhaseEvents
{
    public EventList Load { get; }          // runs during entity.Load()
    public Task<Data> Run(PLangContext context);  // runs during entity.RunAsync()
    public void Add(EventBinding binding);          // adds a runtime binding
}

// Before and After for each entity
public sealed class EntityEvents
{
    public PhaseEvents Before { get; }
    public PhaseEvents After { get; }
}
```

### Navigation reads naturally

```csharp
goal.Events.Before.Load.Run(context)    // "goal's events, before, load phase — run them"
goal.Events.Before.Run(context)         // "goal's events, before runtime — run them"
goal.Events.After.Load.Run(context)     // "goal's events, after, load phase — run them"
step.Events.Before.Run(context)         // "step's events, before runtime — run them"
step.Events.After.Run(context)          // "step's events, after runtime — run them"
```

### Load events are populated from context

At load time, matching event bindings are copied from the global event collection (on System/User event scopes) to the entity's `Events`:

```csharp
// In GoalMethods.Load():
context.PopulateLoadEvents(Events, EventType.OnBeforeGoalLoad, EventType.OnAfterGoalLoad);

// In StepMethods.Load():
context.PopulateLoadEvents(Events, EventType.OnBeforeStepLoad, EventType.OnAfterStepLoad);

// In ActionMethods.Load():
context.PopulateLoadEvents(Events, EventType.OnBeforeActionLoad, EventType.OnAfterActionLoad);
```

### Execution flow with events

```
Load phase:
  context.PopulateLoadEvents(goal.Events, ...)
  goal.Events.Before.Load.Run(context)
    step.Events.Before.Load.Run(context)
      action.Events.Before.Load.Run(context)
      action.Events.After.Load.Run(context)
    step.Events.After.Load.Run(context)
  goal.Events.After.Load.Run(context)

Run phase:
  goal.Events.Before.Run(context)          -- before-goal runtime events
    step.Events.Before.Run(context)        -- before-step runtime events
      action.RunAsync(engine, context)     -- actual execution
    step.Events.After.Run(context)         -- after-step runtime events
  goal.Events.After.Run(context)           -- after-goal runtime events
```

## 5. Handlers Navigate Through Engine

Handlers extend `BaseClass<TParams>` and receive `Engine` and `Context` via `Initialize()`. They navigate to system capabilities through the engine.

### IO navigation

Instead of `Console.WriteLine`, handlers write to named channels through `Engine.IO`:

```csharp
// output.write handler
Engine.IO.WriteTextAsync(IO.IO.StdOut, text)

// Reading from a channel
Engine.IO.ReadTextAsync(IO.IO.StdIn)

// Writing structured data (serialized through engine.Serializers)
Engine.IO.WriteAsync("stdout", data, "application/json")
```

IO manages named channels (`stdin`, `stdout`, `stderr`, or custom). Each channel wraps a stream with direction and content type:

```csharp
engine.IO.Get("stdout")                            // get a channel
engine.IO.GetOrCreate("custom", () => channel)     // get or create
engine.IO.CreateMemoryChannel("buffer")             // in-memory
engine.IO.CreateFileChannel("log", path, FileMode.Append)  // file-backed
```

### Serializer navigation

Instead of `JsonSerializer.Serialize(obj)`, handlers use the engine's serializer registry:

```csharp
// Serialize to a stream (used internally by IO.WriteAsync)
engine.Serializers.SerializeAsync(new SerializeOptions
{
    Stream = channel.Stream,
    Data = data,
    ContentType = "application/json"
});

// Deserialize from a file (used by IO.ReadAsync)
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

### Engine (system-level, shared)

```
Engine
  .AppContext         PLangAppContext — app config, global events, serializers, RootPath
  .Actions            ActionRegistry — discover and resolve handlers
  .Serializers        SerializerRegistry — serialize / deserialize
  .Goals              Goals — loaded goals, lookup by name
  .FileSystem         IPLangFileSystem — sandboxed file I/O
  .IO                 IO — channel-based I/O (stdin, stdout, stderr, custom)
  .System / .Service / .User   Actor — trust-level identities (lazy)
  .IsDebugMode        bool
```

### PLangContext (request-level, per-execution)

```
Context
  .Id                 string — unique execution id
  .AppContext          PLangAppContext — back-reference to app context
  .MemoryStack         MemoryStack — all %variables% for this execution
  .CallStack           CallStack? — goal/step call frames, stack trace, errors
  .Goal                Goal? — the goal currently executing
  .Step                Step? — the step currently executing
  .CurrentGoalName     string? — shortcut to current goal name
  .CurrentStepIndex    int? — shortcut to current step index
  .Actor               Actor? — the actor that owns this context
  .Parent              PLangContext? — parent context (for nested calls)
  .Depth               int — nesting depth (0 = root)
  .IsAsync             bool — whether this is an async execution
  .CancellationToken   CancellationToken — linked to AppContext.ShutdownToken
  .CreatedAt           DateTime
  .Duration            TimeSpan
  .System              EventScope — system-level event bindings
  .User                EventScope — user-level event bindings
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

### PLangAppContext (app-level, shared across all requests)

```
AppContext
  .Id                  string — app instance id
  .RootPath            string — app root directory
  .Environment         string — "production", "development", etc.
  .Events              Events — global event collection
  .Serializers         SerializerRegistry
  .IsDebugMode         bool
  .ShutdownToken       CancellationToken
  .StartedAt           DateTime
  .Uptime              TimeSpan
  [key]                object? — arbitrary key-value store (Get<T>, Set<T>, GetOrCreate<T>)
```

### BaseClass convenience properties

Handlers extend `BaseClass<TParams>` which exposes shortcuts so you don't always navigate manually:

```csharp
protected MemoryStack MemoryStack => Context.MemoryStack;
protected PLangAppContext AppContext => Context.AppContext;
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
| Write to stdout | `Engine.IO.WriteTextAsync(IO.StdOut, text)` |
| Read/write files | `Engine.FileSystem.File.ReadAllTextAsync(path)` |
| Call another goal | `Engine.RunGoalAsync(goalName, Context, CancellationToken)` |
| Resolve a handler | `Engine.Actions.GetCodeGenerated("module", "method", Context)` |
| Serialize data | `Engine.Serializers.SerializeAsync(options)` |
| Check current goal/step | `Context.Goal`, `Context.Step` |
| Check call depth | `Context.Depth`, `Context.CallStack.Depth` |
| Detect recursion | `Context.CallStack.ContainsGoal("GoalName")` |
| Get app root path | `Engine.RootPath` or `Context.AppContext.RootPath` |
| Check environment | `Context.AppContext.Environment` |
| Store request-scoped data | `Context["key"] = value` / `Context.Get<T>("key")` |
| Store app-scoped data | `Context.AppContext["key"] = value` |
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
Engine.IO.WriteTextAsync(IO.IO.StdOut, data);
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
    engine.IO ...
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
    public EntityEvents Events => _context?.EventsFor(this) ?? _fallbackEvents;
}

// Correct: Events is a direct property, context passed as parameter
public sealed partial class Goal
{
    public EntityEvents Events { get; } = new();    // per-object, set at load time
}

await Events.Before.Run(context);   // context passed as parameter
```

**Rule**: If something is per-request (like which context is executing), pass it as a parameter. If something is per-object (like which event bindings apply), store it on the object.

### Back-references use `[JsonIgnore]`

```csharp
[JsonIgnore] public Goal? Parent { get; set; }  // Goal -> parent Goal
[JsonIgnore] public Goal? Goal { get; set; }     // Step -> owning Goal
[JsonIgnore] public EntityEvents Events { get; } // not serialized
```

These are set after deserialization (e.g., `step.Goal = goal` during goal loading) and excluded from serialization to avoid circular references.

## 10. Summary

1. **Engine is the root** — everything hangs off it: IO, Goals, Actions, Serializers, FileSystem, Actors
2. **Properties navigate** — `engine.IO.WriteTextAsync(...)`, `engine.Goals.Get(...)`, `goal.Events.Before.Run(...)`
3. **Collections own their loops** — `Steps.Load()`, `Actions.RunAsync()`, not a foreach in Goal or Step
4. **Methods belong on the owner** — Goals loads goals, Steps loads steps, Data merges results
5. **Read like sentences** — `goal.Events.Before.Load.Run(context)` reads naturally
6. **Events are entity-owned** — `EntityEvents` with `PhaseEvents` (Before/After) and phase split (Load/Runtime)
7. **Handlers navigate through Engine** — IO channels, FileSystem, Serializers are all reachable from Engine
8. **No unnecessary DTOs** — use real types inside the runtime, DTOs only at serialization boundaries
9. **Don't cache request state on shared objects** — pass `PLangContext` as a parameter, don't store it
10. **Keep object references** — `StepError.Step`, not `StepError.StepText`
11. **Data owns merge** — `Data.Merge()` combines `List<Data>` by name
12. **Don't create wrapper objects** — if the data is on `PLangContext`, pass `PLangContext`
