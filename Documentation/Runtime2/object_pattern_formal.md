# The Object-Based Pattern (OBP)

## The Problem: Friction

Consider what happens when you save user data in a traditional codebase. A controller receives a request. It extracts fields. It passes them to a service. The service validates, transforms, calls a repository. The repository maps to an entity, calls a database. Every layer knows the shape of the data. Every layer has parameters threaded through it. Every layer adds code, adds coupling, adds CPU cycles.

Now count: how many lines of code? How many method signatures? How many objects created just to move data from A to B?

That is friction. Every parameter pass, every service layer, every eager load, every abstraction that exists "just in case" — friction. Wasted CPU. Wasted complexity. Wasted context for anyone reading the code, human or LLM.

Traditional architecture says: know everything, everywhere. Every method signature declares exactly what it needs. Every constructor loads what it might use. Every layer transforms data into its own shape.

OBP says: stop. Most of that work is unnecessary.

## The Insight: Data Flows Blind

In PLang, a program looks like this:

```
- read file.txt, write to %data%
- store %data%
- write out %data%
```

Three lines. Read a file, stored it in a database, printed it out. The programmer has no idea what's in `%data%`. It's just there. It flows.

This is not a limitation — it's a design principle. The caller doesn't need to know the shape of the data. Only the endpoint that actually works with the data needs to know.

So when building the C# runtime for PLang, the question became: **what if the runtime worked the same way?**

What if incoming data is always just `Data`? You don't know what's inside. You don't need to. You receive it, you pass it along, you let the thing that actually owns it do the work.

If only one place knows the shape, then that place must own all the logic. There's nowhere else to put it.

## The Consequence: Objects Own Everything

Take a `Goal` class. In traditional architecture, it's a DTO — maybe a `Name` property, a `ToString()` override. All the real work happens in a `GoalService` or `GoalManager` or `GoalProcessor` somewhere else.

In OBP, Goal gets `Load()` and `Save()`. The middle layer disappears:

```csharp
var data = request.Form["data"];
app.Goal.Save(data);
```

Two lines. The middle layer that used to be hundreds of lines of parameter threading, validation chains, and service orchestration — gone. And look what `app.Goal.Save` tells you: you're saving a goal. Beautiful. Readable. Navigate from the root, arrive at what you need.

"But you just moved the logic!" — yes. Look where. The object that owns the data now owns the behavior. The thing that knows what a Goal looks like is the only thing that touches Goal internals. Everything else just passes it along.

## Friction to Zero: Lazy Everything

Consider a file path:

```csharp
var path = new Path("/file.txt");
path.Size  // how big is the file?
```

Traditional code:

```csharp
public class Path
{
    private FileInfo _fileInfo;

    public Path(string path)
    {
        _fileInfo = new FileInfo(path);  // loads size, dates, attributes — everything
    }
}
```

All that info is loaded in the constructor. But we never asked for it. We just wanted a path. Loading everything costs CPU. There is friction. We wasted cycles on something we might never need.

In OBP, `new Path("/file.txt")` stores only the string. Nothing else happens. When you access `path.Size`, *then* it does the work. Only the CPU that's needed, only when it's needed.

Friction goes to almost zero. Four references in memory, maybe 10 nanoseconds. Compare that to what traditional code does just to save user info — how many objects constructed, how many fields populated, how many layers traversed.

## The Root: One Entry Point

OBP requires a root object. In PLang Runtime2, it's `Engine`. Everything hangs off it:

```
Engine
  .Goals         — loaded goals, lookup by name
  .Libraries     — discovers and resolves handlers
  .Serializers   — serialize/deserialize
  .FileSystem    — sandboxed file operations
  .Channels      — named I/O routing (stdin, stdout, stderr, custom)
  .Events        — app-level event collection
  .Cache         — pluggable step cache
```

Any code that has `Engine` can reach anything. No dependency injection frameworks. No parameter lists that grow every time you need one more thing. Navigate to what you need:

```csharp
Engine.Channels.WriteTextAsync(StdOut, text);
Engine.FileSystem.File.ReadAllTextAsync(path);
Engine.Goals.Get("Start");
```

Read it like English: "the engine's channels — write text to stdout." "The engine's file system's file — read all text." The code tells you exactly what it's doing and where it lives.

## The Rules

These rules aren't arbitrary — they all derive from the same insight: minimize friction, let objects own their data, defer everything until needed.

### 1. Behavior belongs to the owner

Every operation belongs to the object whose data it acts on. If it iterates a collection, it belongs on the collection. If it transforms a result, it belongs on the result type.

**Test**: "Whose data does this method touch?" That's the owner.

```csharp
// Correct: Steps owns the step list, so Load belongs here
goal.Steps.Load(context);

// Wrong: the loop belongs on Steps, not on whoever calls this
foreach (var step in goal.Steps)
    await step.Load(context);
```

Parents delegate. They never iterate their children directly:

```csharp
public async Task Load(PLangContext context)
{
    await Lifecycle.Before.Run(context);
    await Steps.Load(context);              // delegates, does not loop
    await Lifecycle.After.Run(context);
}
```

### 2. Navigate, don't pass

Reach dependencies through the object graph. Never decompose an object into separate parameters:

```csharp
// Wrong: passing each thing separately
async Task RunStep(Engine engine, IO io, Goals goals) { }

// Correct: reach them through engine
async Task RunStep(Engine engine)
{
    engine.Channels ...
    engine.Goals ...
}
```

This applies to the caller too. If a handler calls `Path.Delete(recursive, ignoreIfNotFound)`, it's decomposing itself into parameters. The OBP form is `Path.Delete(actionRecord)` — let the callee navigate the action record for what it needs.

**Why**: Coupling stays one-directional. The callee knows about the caller's structure, but the caller doesn't know what the callee needs. If the callee needs a new property later, only the callee changes.

### 3. Names describe what the object IS

Property names are nouns. They tell you what the thing is, not what it does. When you look at the object graph, the name alone tells you where to navigate.

```
engine.Goals        — "Goals" manages goals
engine.Channels     — "Channels" manages I/O channels
engine.FileSystem   — "FileSystem" manages file access
```

Not:
```
engine.IO           — IO of what? Files? Channels?
engine.Run          — is this a method or an object?
engine.Data         — what data?
```

**Test**: If the name could describe two different things, it's too broad.

Structures are things. A `Lifecycle` with `.Before` and `.After` IS a lifecycle — don't rename it to `EventManager`. Properties are nouns, methods are verbs — `lifecycle.Before.Run()`, not `lifecycle.Load.Before`.

### 4. Keep object references, not extracted fields

Store `Step`, not `step.Text`. Store `Goal`, not `goal.Name`. Decomposing objects into primitives discards information.

```csharp
// Wrong: decomposing Step into a string
public class StepError { public string StepText { get; init; } }

// Correct: keeping the reference
public class StepError { public Step? Step { get; init; } }
```

Wrapper DTOs are only allowed at serialization boundaries.

### 5. Collections own their loops

Collection types inherit `List<T>` and add domain operations. Parents delegate — they never iterate directly:

```csharp
public sealed class Steps : List<Step>
{
    public async Task Load(PLangContext context)
    {
        foreach (var step in this)
            await step.Load(context);
    }
}

public sealed class Actions : List<Action>
{
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

### 6. Request state is a parameter, never stored

Goal and Step objects may be shared between threads. Storing `PLangContext` on them creates race conditions.

```csharp
// Wrong: _context is shared, multiple threads overwrite it
public sealed partial class Goal
{
    internal PLangContext? _context;
}

// Correct: context passed as parameter
await goal.Steps.Load(context);
await Lifecycle.Before.Run(context);
```

**Rule**: If something is per-request, pass it. If something is per-object, store it.

### 7. Data flows — relay, don't repackage

`Data` is created at the boundary where the value originates. Once created, it flows through every layer unchanged. Intermediate layers may inspect it (`.Success`, `.Error`) but never decompose it and rebuild it.

```csharp
// Wrong: extracting and rewrapping
return Data.Ok(result.Value);   // loses Type, Properties, Signature

// Correct: relay as-is
return result;
```

Data owns its own composition through `Merge()`. The collection calls merge — it never inspects the data structure directly.

### 8. No redundant wrappers

If the data a callee needs already exists on an object the caller has, pass that object. Don't create a new class that copies fields into a different shape.

```csharp
// Wrong: EventContext duplicates what PLangContext already has
var result = await Events.Before.RunAsync(new EventContext
{
    GoalName = context.CurrentGoalName,
    StepIndex = context.CurrentStepIndex,
});

// Correct: PLangContext already has everything
var result = await Events.Before.Run(context);
```

## Why This Matters for LLMs

An LLM reading OBP code can traverse the object graph like a map. `Engine.Goals.Get("Start")` — it knows exactly where goals live. `step.Actions.RunAsync(engine, context)` — it knows actions own their execution.

Traditional architecture scatters behavior across services, managers, and utilities. An LLM (or a human) must hold the entire service graph in context to understand what happens when you save a goal. More context, worse results.

OBP collapses that context. The object graph IS the architecture. Navigate to what you need. Read it like English. Every object does its own work.

## Entity Hierarchy (PLang Runtime2)

```
Goal
  .Name             — string
  .Steps            — Steps (collection wrapper)
  .Lifecycle        — Lifecycle (Before/After bindings)
  .Path / .PrPath   — file system locations
  .Parent           — Goal? (back-reference)

Step
  .Index            — int
  .Text             — string (the PLang instruction)
  .Actions          — Actions (collection wrapper)
  .Lifecycle        — Lifecycle
  .Goal             — Goal? (back-reference)

Action
  .Class            — string (module name, e.g. "variable")
  .Method           — string (action name, e.g. "set")
  .Parameters       — List<Data>
  .Return           — List<Data>?
  .Lifecycle        — Lifecycle
```

Navigation reads naturally:
```csharp
goal.Steps[0]                   // "goal's first step"
step.Actions[0].Class           // "step's first action's class"
goal.Steps[0].Actions[0]        // full chain from goal to action
```

## Events: Entity-Owned Lifecycle

Every entity (Goal, Step, Action) has the same `Lifecycle` structure:

```csharp
public sealed class Lifecycle
{
    public Bindings Before { get; }
    public Bindings After { get; }
}
```

Execution flow:
```
goal.Lifecycle.Before.Run(context)
  step.Lifecycle.Before.Run(context)
    action.Lifecycle.Before.Run(context)
    action.RunAsync(engine, context)
    action.Lifecycle.After.Run(context)
  step.Lifecycle.After.Run(context)
goal.Lifecycle.After.Run(context)
```

## Handlers Navigate Through Engine

Handlers extend `BaseClass<TParams>` and receive `Engine` and `Context` via `Initialize()`. They navigate to capabilities:

```csharp
// Writing to a channel
Engine.Channels.WriteTextAsync(IO.Channels.StdOut, text);

// Reading a file
Engine.FileSystem.File.ReadAllTextAsync(path);

// Calling another goal
Engine.RunGoalAsync(goalName, Context, CancellationToken);

// Resolving a handler
Engine.Libraries.GetCodeGenerated("variable", "set", Context);

// Serializing
Engine.Serializers.SerializeAsync(options);
```

### Handler naming conventions

| Element | Convention | Example |
|---------|-----------|---------|
| **Record** (parameters) | lowercase action name | `set`, `save`, `@if` |
| **Handler** (execution) | PascalCase + `Handler`, `partial` | `SetHandler`, `IfHandler` |
| **Namespace** | `PLang.Runtime2.modules.{module}` | `modules.condition` |
| **Registry key** | `{module}.{record}` | `condition.if` |

## Context and Engine: What You Can Access

### Engine (system-level)

```
Engine
  .Goals              Goals — loaded goals
  .Libraries          Libraries — handler resolution
  .Serializers        SerializerRegistry
  .FileSystem         IPLangFileSystem — sandboxed I/O
  .Channels           Channels — named streams
  .Events             Events — app-level events
  .Cache              ICache
  .System/.Service/.User   Actor — trust levels (lazy)
  .Path               string — always "/"
  .AbsolutePath       string — OS path
  .Environment        string — "production", etc.
  .Property           Property — key-value store
```

### PLangContext (request-level)

```
Context
  .Id                 string — unique execution id
  .Engine             Engine — back-reference
  .MemoryStack        MemoryStack — all %variables%
  .CallStack          CallStack? — frames, errors
  .Goal               Goal? — currently executing
  .Step               Step? — currently executing
  .Actor              Actor?
  .Parent             PLangContext? — for nested calls
  .CancellationToken  CancellationToken
```

### Quick reference

| Need | Navigation |
|------|-----------|
| Read/write a variable | `MemoryStack.Get("name")` / `.Set("name", value)` |
| Write to stdout | `Engine.Channels.WriteTextAsync(StdOut, text)` |
| Read/write files | `Engine.FileSystem.File.ReadAllTextAsync(path)` |
| Call another goal | `Engine.RunGoalAsync(goalName, Context, ct)` |
| Resolve a handler | `Engine.Libraries.GetCodeGenerated("module", "method", Context)` |
| Check environment | `Engine.Environment` |
| Store request data | `Context["key"] = value` |
| Store app data | `Engine.Property["key"] = value` |

## Common OBP Violations

If you're doing any of these, stop:

1. **Iterating someone else's collection** — the loop belongs on the collection
2. **Passing extracted fields instead of the object** — pass the root, navigate from there
3. **Eager loading in constructors** — defer until accessed
4. **Creating a service/manager class** — put the method on the object that owns the data
5. **Wrapping data into a new DTO** — pass the existing object
6. **Extracting `.Value` from Data to rewrap it** — relay the Data as-is
7. **Caching context on a shared object** — pass it as a parameter

The fix progression:
1. Create the type, use it everywhere (basic plumbing)
2. Move behavior to the owner (behavior belongs to owner)
3. Store the root, navigate internally (object graph navigation)
4. Pass the caller as a whole (don't decompose the caller)

Each step feels "done" but may still violate OBP at the next level. The test: are you pulling fields out of an object to hand them individually to another method? If yes, pass the object instead.
