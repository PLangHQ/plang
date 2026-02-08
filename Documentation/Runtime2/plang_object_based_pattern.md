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
    protected override async Task<Return> ExecuteAsync(write p)
    {
        return await Engine.IO.WriteTextAsync(IO.IO.StdOut, p.content?.ToString());
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

    public async Task<Return> RunAsync(Engine engine, PLangContext context, CancellationToken ct = default)
    {
        Return merged = new();
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
    public async Task<Return> Run(PLangContext context);
}

// Two phases: Load-time events and Runtime events
public sealed class PhaseEvents
{
    public EventList Load { get; }          // runs during entity.Load()
    public Task<Return> Run(PLangContext context);  // runs during entity.RunAsync()
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

## 6. Return Owns Its Composition

When multiple actions produce results, the merge logic lives on `Return` — it knows how to combine `List<Data>` by name:

```csharp
public class Return
{
    public object? Value { get; set; }
    public IError? Error { get; set; }
    public bool Success => Error == null;

    public Return Merge(Return other)
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

        return new Return { Value = myData };
    }
}
```

The `Actions` collection uses `Merge` in its loop — it never inspects the return structure directly.

## 7. Do and Don't

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

## 8. Context Rules

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

## 9. Summary

1. **Engine is the root** — everything hangs off it: IO, Goals, Actions, Serializers, FileSystem, Actors
2. **Properties navigate** — `engine.IO.WriteTextAsync(...)`, `engine.Goals.Get(...)`, `goal.Events.Before.Run(...)`
3. **Collections own their loops** — `Steps.Load()`, `Actions.RunAsync()`, not a foreach in Goal or Step
4. **Methods belong on the owner** — Goals loads goals, Steps loads steps, Return merges returns
5. **Read like sentences** — `goal.Events.Before.Load.Run(context)` reads naturally
6. **Events are entity-owned** — `EntityEvents` with `PhaseEvents` (Before/After) and phase split (Load/Runtime)
7. **Handlers navigate through Engine** — IO channels, FileSystem, Serializers are all reachable from Engine
8. **No unnecessary DTOs** — use real types inside the runtime, DTOs only at serialization boundaries
9. **Don't cache request state on shared objects** — pass `PLangContext` as a parameter, don't store it
10. **Keep object references** — `StepError.Step`, not `StepError.StepText`
11. **Return owns merge** — `Return.Merge()` combines `List<Data>` by name
12. **Don't create wrapper objects** — if the data is on `PLangContext`, pass `PLangContext`
