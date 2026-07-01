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

OBP requires a root object. In PLang App, it's `App`. Everything hangs off it:

```
App
  .Goals         — loaded goals, lookup by name
  .Libraries     — discovers and resolves handlers
  .Serializers   — serialize/deserialize
  .FileSystem    — sandboxed file operations
  .Channels      — named I/O routing (stdin, stdout, stderr, custom)
  .Events        — app-level event collection
  .Cache         — pluggable step cache
```

Any code that has `App` can reach anything. No dependency injection frameworks. No parameter lists that grow every time you need one more thing. Navigate to what you need:

```csharp
app.Channels.WriteTextAsync(StdOut, text);
app.FileSystem.File.ReadAllTextAsync(path);
app.Goals.Get("Start");
```

Read it like English: "the app's channels — write text to stdout." "The app's file system's file — read all text." The code tells you exactly what it's doing and where it lives.

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
async Task RunStep(App app, IO io, Goals goals) { }

// Correct: reach them through app
async Task RunStep(App app)
{
    app.Channels ...
    app.Goals ...
}
```

This applies to the caller too. If a handler calls `Path.Delete(recursive, ignoreIfNotFound)`, it's decomposing itself into parameters. The OBP form is `Path.Delete(actionRecord)` — let the callee navigate the action record for what it needs.

**Why**: Coupling stays one-directional. The callee knows about the caller's structure, but the caller doesn't know what the callee needs. If the callee needs a new property later, only the callee changes.

### 3. Names describe what the object IS

Property names are nouns. They tell you what the thing is, not what it does. When you look at the object graph, the name alone tells you where to navigate.

```
app.Goals        — "Goals" manages goals
app.Channels     — "Channels" manages I/O channels
app.FileSystem   — "FileSystem" manages file access
```

Not:
```
app.IO           — IO of what? Files? Channels?
app.Run          — is this a method or an object?
app.Data         — what data?
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

### 5. Collections are the API

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
    public async Task<Data> RunAsync(App app, PLangContext context, CancellationToken ct = default)
    {
        Data merged = Data.Ok();
        foreach (var action in this)
        {
            var result = await action.RunAsync(app, context, ct);
            if (!result.Success) return result;
            merged = merged.Merge(result);
        }
        return merged;
    }
}
```

**Expose the collection, don't proxy it.** If an object owns a collection, the collection itself is the interface. Don't wrap `Add`, `Remove`, `Get` in owner methods — that creates a middleman that hides what's actually happening.

```csharp
// Wrong: owner proxies the collection
public class CallStack
{
    private readonly List<IError> _errors = new();
    public void AddError(IError error) => _errors.Add(error);
    public IReadOnlyList<IError> GetErrors() => _errors;
    public void ClearErrors() => _errors.Clear();
}

// Correct: collection is the API
public class CallStack
{
    public List<IError> Errors { get; } = new();
}

// Callers use the collection directly
callStack.Errors.Add(error);
callStack.Errors.Clear();
var last = callStack.Errors.Last();
```

The owner's job is to *have* the collection, not to *proxy* it. When the collection needs domain-specific operations (like `Steps.Load` or `Actions.RunAsync`), those belong on the collection type itself — still not on the parent.

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

**Even a legitimate new `Data` holds the built value whole — never re-Build it.** When you genuinely must construct a new `Data` (a typed view via `Data<T>.From`, a copy, a retype), carry the source's *already-built* value instance as-is (`SetValueDirect(source.Instance)`) and born the wrapper value-less **with the source's context**. Do NOT reconstruct through the `(name, value, type)` ctor — that re-runs `type.Build`, which (a) **re-resolves** a `%var%`/template value (breaking store-as-is: `variable.set` must keep the reference, not render it — the value renders on its own door, at read) and (b) re-types an already-built value. `Build`/`Create` run exactly ONCE, at the boundary where the value originates; a copy or retype is not a second origination. (This is why `Data<T>.From` borns value-less with `source.Context` and holds `source.Instance` directly, rather than rebuilding via the value ctor.)

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

### 9. Only leaves touch `Data.Value`

`Data` rides through the runtime as a closed package: `{Type, Value, Properties, Signature}`. The runtime is a courier — it moves the package, reads the routing key (`Data.Type`), inspects success/error state, but never opens it. `Data.Value` is opaque to every layer between where it's produced and where it's consumed.

Two surfaces are leaves. Everything else is courier.

1. **Leaf actions.** A handler that declares `Data<image> A { get; init; }` is opening the package — it has named the type and is going to read bytes. `math.add` opens up `number`; `image.resize` opens up bytes + mime; `output.write` does **not** open the package, it forwards.
2. **Leaf serializers.** When a value reaches a channel, the channel asks the value to render itself for that format. `image` for `text/plain` renders as a path placeholder; for `text/html` as `<img>` markup; for `application/plang` or `application/json` as base64 string; for `application/protobuf` as raw bytes. Same instance, four wire shapes — the type owns the mapping, not the channel.

Everything else — variable memory, callstack frames, goal-to-goal handoff, channel routing, signing, the wire envelope — sees only the package, not the contents. They read `Data.Type` to route; they read `.Success` / `.Error` to gate; they never reach for `.Value`.

```csharp
// Wrong: courier opens the package mid-flight
public Task<Data> Run() {
    if (input.Value is Image img) {            // courier became a leaf
        return DoSomething(img.Bytes);
    }
    return input;  // and now lies about being a relay
}

// Wrong: a LEAF this time — but it decomposes. It cracks the operand carriers
// open and hands raw fields to a free function. Reading your own typed slot is
// fine; chopping it into primitives for a static helper is the smell.
public Task<Data<image>> Run() {
    var img = await A.Value();                  // ok so far — a leaf may read its own value
    var w = await Width.Value(); var h = await Height.Value();  // SMELL: cracking the other carriers
    return Data<image>.Ok(Resize(img.Bytes, w, h));            // SMELL: value handed to a free function
}

// Correct: name the type at the leaf, then the VALUE does the work —
// operands are passed as whole carriers, never extracted.
public Task<Data<image>> Run() => await A.Resize(Width, Height);
//   the image resizes itself; Width/Height ride in as whole Data carriers; returns Data<image>
```

A leaf is allowed to read its own typed value — but it must not **decompose** it. The value owns its operations: call the operation on the carrier and pass other operands as whole carriers (`A.Resize(Width, Height)`, `Value.Round(Decimals)`, `A.Add(B)`), never extract `.Value()` and feed raw fields to a free function or a static helper (`Resize(img.Bytes, …)`, `number.Round(n, …)`, `number.Add(a, b, …)`). Decomposing at the call site is the same mistake as Rule #2 (don't decompose an object into parameters) and Rule #4 (keep the reference) — now at the value layer. The tell: a leaf that `await X.Value()`s an operand only to hand the raw inside to something else. If you opened the box to pass what was inside, pass the box.

This is what makes adding a new PLang type cheap. A type registers its routing key (its name), declares its leaf-action surface (`Data<image>` parameter slots on handlers), and declares its leaf-serializer behavior (`IWireWritable` or the equivalent on the value class). Nothing in the courier path changes. The first new type that needs touching variable memory, callstack, or channel routing means the type system has grown a leak.

The rule is a sharpening of Rule #7: don't repackage `Data`, *and* don't open it mid-flight to peek at `Value`, *and* — even at a leaf — don't decompose the value to operate on it. The failures look different at the call site but they are the same architectural mistake: data pulled out of the object that owns it.

## Why This Matters for LLMs

An LLM reading OBP code can traverse the object graph like a map. `app.Goals.Get("Start")` — it knows exactly where goals live. `step.Actions.RunAsync(app, context)` — it knows actions own their execution.

Traditional architecture scatters behavior across services, managers, and utilities. An LLM (or a human) must hold the entire service graph in context to understand what happens when you save a goal. More context, worse results.

OBP collapses that context. The object graph IS the architecture. Navigate to what you need. Read it like English. Every object does its own work.

## Entity Hierarchy (PLang App)

```
Goal
  .Name             — string
  .Steps            — Steps (collection wrapper)
  .Events           — Events (.Before / .After bindings)
  .Path / .PrPath   — file system locations
  .Parent           — Goal? (back-reference)

Step
  .Index            — int
  .Text             — string (the PLang instruction)
  .Actions          — Actions (collection wrapper)
  .Events           — Events (.Before / .After bindings)
  .Goal             — Goal? (back-reference)

Action
  .Module           — string (module name, e.g. "variable")
  .ActionName       — string (action name, e.g. "set")
  .Parameters       — List<Data>
  .Return           — List<Data>?
  .Events           — Events (.Before / .After bindings)
  .Step             — Step? (back-reference)
```

Navigation reads naturally:
```csharp
goal.Steps[0]                   // "goal's first step"
step.Actions[0].Module          // "step's first action's module"
goal.Steps[0].Actions[0]        // full chain from goal to action
```

## Events: Entity-Owned

Every entity (Goal, Step, Action) has an `Events` property with `.Before` and `.After`:

```csharp
public class Events : IContext
{
    public List<GoalCall> Before { get; }   // bindings that run before
    public List<GoalCall> After { get; }    // bindings that run after
}
```

Context is injected via `IContext` — events resolve their bindings lazily from the execution context.

Execution flow:
```
goal.Events.Before → run each binding
  step.Events.Before → run each binding
    action.Events.Before → run each binding
    action → handler.ExecuteAsync(action, app, context)
    action.Events.After → run each binding
  step.Events.After → run each binding
goal.Events.After → run each binding
```

## Handlers

Handlers implement `ICodeGenerated` via source generation. They receive the action record, app, and context:

```csharp
public interface ICodeGenerated
{
    Task<Data> ExecuteAsync(Action action, App app, PLangContext context);
}
```

They navigate to capabilities through app:

```csharp
// Writing to a channel
app.Channels.WriteTextAsync(StdOut, text);

// Reading a file
app.FileSystem.File.ReadAllTextAsync(path);

// Calling another goal
app.RunGoalAsync(goalCall, context, ct);

// Resolving a handler
app.Modules.GetCodeGenerated("variable", "set", context);
```

### Handler naming conventions

| Element | Convention | Example |
|---------|-----------|---------|
| **Record** (parameters) | lowercase action name | `set`, `save`, `@if` |
| **Handler** (execution) | PascalCase + `Handler`, `partial` | `SetHandler`, `IfHandler` |
| **Namespace** | `app.module.{module}` | `app.module.condition` |
| **Registry key** | `{module}.{record}` | `condition.if` |

## Context and App: What You Can Access

### App (system-level)

```
App
  .Goals              EngineGoals — loaded goals
  .Modules            EngineModules — handler resolution
  .FileSystem         IPLangFileSystem — sandboxed I/O
  .Channels           EngineChannels — named streams
  .Events             EngineEvents — app-level event bindings
  .Cache              ICache — step-level caching
  .Building           Build — builder state (IsEnabled, Files)
  .Debug              Debugging — debug configuration
  .System/.Service/.User   Actor — trust levels (lazy)
  .CurrentActor       Actor — active actor
  .Path               string — always "/"
  .AbsolutePath       string — OS path
  .Environment        string — "production", etc.
```

### PLangContext (request-level)

```
Context
  .Id                 string — unique execution id
  .App             App — back-reference
  .Variables        Variables — all %variables%
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
| Read/write a variable | `context.Variables.Get("name")` / `.Set("name", value)` |
| Write to stdout | `app.Channels.WriteTextAsync(StdOut, text)` |
| Read/write files | `app.FileSystem.File.ReadAllTextAsync(path)` |
| Call another goal | `app.RunGoalAsync(goalCall, context, ct)` |
| Resolve a handler | `app.Modules.GetCodeGenerated("module", "action", context)` |
| Check environment | `app.Environment` |

## Common OBP Violations

If you're doing any of these, stop:

1. **Iterating someone else's collection** — the loop belongs on the collection
2. **Passing extracted fields instead of the object** — pass the root, navigate from there
3. **Eager loading in constructors** — defer until accessed
4. **Creating a service/manager class** — put the method on the object that owns the data
5. **Wrapping data into a new DTO** — pass the existing object
6. **Extracting `.Value` from Data to rewrap it** — relay the Data as-is
7. **Caching context on a shared object** — pass it as a parameter
8. **Proxying a collection through wrapper methods** — expose the collection, let callers use it directly

The fix progression:
1. Create the type, use it everywhere (basic plumbing)
2. Move behavior to the owner (behavior belongs to owner)
3. Store the root, navigate internally (object graph navigation)
4. Pass the caller as a whole (don't decompose the caller)

Each step feels "done" but may still violate OBP at the next level. The test: are you pulling fields out of an object to hand them individually to another method? If yes, pass the object instead.
