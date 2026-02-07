# Object Based Pattern

The Object Based Pattern is how Runtime2 organizes code. Instead of putting logic in manager classes or service layers, behavior lives on the objects that own the data. You navigate to what you want through properties, and call methods there.

## The Core Idea

Everything reads like a sentence. You start from a root object and navigate through properties to reach the thing you want to operate on.

```
engine.Goals.Get("Start")        -- get a goal from the engine
step.Actions.Load(context)       -- load all actions on a step
context.Events.Step.Before       -- get "before" events for steps
goal.Steps.Load(context)         -- load all steps on a goal
engine.IO.WriteAsync("stdout")   -- write to a channel through engine
```

The pattern is: **Subject.Thing.Operation**

## Engine at the Top

Engine is the root. Everything hangs off it as properties:

```
engine
  .Goals          -- collection of goals, knows how to load/find them
  .Actions        -- registry of action handlers
  .IO             -- channel-based I/O
  .FileSystem     -- file operations
  .Serializers    -- serialization registry
  .System         -- system actor (high trust)
  .User           -- user actor (low trust)
  .Service        -- service actor (mid trust)
```

Each property is an object that **does things**. Goals knows how to load from files. IO knows how to read/write channels. They are not passive data bags.

Every object that needs the engine gets a reference to it. A handler can access `engine.IO`, `engine.Goals`, `engine.FileSystem` -- everything is reachable from the root.

## Collections Own Their Iteration

When you have a collection, the collection owns the operations on its items.

```csharp
// Steps owns the Load loop
public sealed class Steps : List<Step>
{
    public async Task Load(PLangContext context)
    {
        foreach (var step in this)
            await step.Load(context);
    }
}

// Actions owns its Load loop
public sealed class Actions : List<Action>
{
    public async Task Load(PLangContext context)
    {
        foreach (var action in this)
            await action.Load(context);
    }
}
```

The parent just delegates:

```csharp
// Goal.Load() -- clean, one line
public async Task Load(PLangContext context)
{
    await Steps.Load(context);
}

// Step.Load() -- clean, one line
public async Task Load(PLangContext context)
{
    await Actions.Load(context);
}
```

## Properties Read Like Sentences

Event access should read naturally. You're asking: "give me the before events for a step."

```
context.Events.Step.Before       -- YES: reads like English
context.Events.OnBeforeStepLoad  -- NO: Hungarian-style naming, verbose
```

More examples:

```
engine.Goals.Get("Start")       -- YES: "engine's goals, get Start"
engine.GetGoal("Start")         -- NO: engine isn't a goal manager

goal.Steps[0]                   -- YES: "goal's first step"
goal.GetStep(0)                 -- NO: unnecessary method

step.Actions.Load(context)      -- YES: "step's actions, load them"
step.LoadActions(context)       -- NO: step isn't an action loader
```

## Do and Don't

### DO: Put behavior on the object that owns the data

```csharp
// The collection knows how to load itself
goal.Steps.Load(context);
step.Actions.Load(context);

// The registry knows how to find handlers
engine.Actions.Get("variable", "set");

// Goals knows how to load from files
engine.Goals.LoadFromFileAsync(fileSystem, path);
```

### DON'T: Put behavior on a parent or manager that doesn't own the data

```csharp
// Don't iterate someone else's collection
foreach (var step in goal.Steps)        // NO -- this loop belongs on Steps
    await step.Load(context);

foreach (var action in step.Actions)    // NO -- this loop belongs on Actions
    await action.Load(context);

// Don't make engine do what Goals does
engine.LoadGoal(path);                  // NO -- Goals owns goal loading
```

### DO: Navigate to what you need through properties

```csharp
engine.IO.WriteAsync("stdout", data);
engine.Goals.Get("Start");
context.Events.Step.Before;
engine.User.Context.MemoryStack.GetValue("name");
```

### DON'T: Pass everything through parameters

```csharp
// NO: passing IO, goals, events separately
async Task RunStep(Engine engine, IO io, Goals goals, EventCollection events) { }

// YES: reach them through engine
async Task RunStep(Engine engine)
{
    engine.IO ...
    engine.Goals ...
}
```

### DO: Keep methods focused on what the object does

```csharp
// Goal.RunAsync -- runs the goal (its job)
// Goal.Load     -- loads the goal (its job)
// Goal.ToText   -- represents itself as text (its job)
```

### DON'T: Add methods that belong on a different object

```csharp
// NO: Goal shouldn't know about file I/O
goal.SaveToFile(path);

// YES: Goals (the collection/registry) handles persistence
engine.Goals.LoadFromFileAsync(fileSystem, path);
```

### DO: Use simple inheritance for typed collections

```csharp
public sealed class Steps : List<Step> { }
public sealed class Actions : List<Action> { }
```

`List<T>` is fine when:
- It's an ordered sequence
- Nobody casts it to `List<T>` (it's always used as `Steps` or `Actions`)
- You just need to add methods, not hook into Add/Remove

### DON'T: Create DTOs for everything

If data flows from A to B and they're in the same process, just use the real type. DTOs are for boundaries (serialization, external APIs). Inside the runtime, pass the actual objects.

### DON'T: Put unrelated logic in a method

A method should do one thing. If `Load()` loads, it shouldn't also set up events, validate, and log. Each of those is a separate concern that belongs somewhere else (events on the event system, validation on the object itself, logging on a logger).

```csharp
// NO: doing too much
public async Task Load(PLangContext context)
{
    _context = context;
    ValidateSteps();
    SetupEventBindings();
    LogLoadStart();
    foreach (var step in Steps) await step.Load(context);
    LogLoadEnd();
    NotifyLoaded();
}

// YES: just load
public async Task Load(PLangContext context)
{
    await Steps.Load(context);
}
```

## The Value Property Convention

Every collection wrapper exposes a `Value` property that returns the underlying data. This gives a consistent way to get "the thing inside" any wrapper:

```csharp
goal.Steps.Value        -- List<Step>  (Steps IS the list, Value returns this)
step.Actions.Value      -- List<Action>
engine.Goals.Value      -- IReadOnlyList<Goal>
```

## Don't Cache Context on Shared Objects

Goal and Step objects are shared between threads. Storing a reference to `PLangContext` on them creates a race condition — multiple threads running the same goal overwrite each other's context.

### DON'T: Store request-scoped state on shared objects

```csharp
// NO: _context is shared, multiple threads overwrite it
public sealed partial class Goal
{
    internal PLangContext? _context;

    public ObjectEvents Events => _context?.EventsFor(this) ?? _fallbackEvents;
}

// Then in RunAsync:
_context = context;  // RACE: thread A sets this, thread B overwrites it
```

### DO: Put state directly on the object, or pass context as a parameter

```csharp
// YES: Events is a direct property, no context needed
public sealed partial class Goal
{
    public ObjectEvents Events { get; } = new();
}

// Resolve events at load time, store them on the object:
goal.Events.Add(binding);

// Pass context as a parameter where needed:
await Events.Before.Run(context);
```

The rule: if something is per-request (like which context is executing), pass it as a parameter. If something is per-object (like which event bindings apply), store it on the object. Never use a shared object as a cache for request-scoped data.

## Let Objects Carry Their Own Data

When error context is available, store the actual object — don't decompose it into strings.

### DON'T: Extract fields from an object just to store them separately

```csharp
// NO: extracting Text from Step, creating extra objects
var error = StepError.FromException(ex, context);
error = new StepError(error.Message, context, error.Key, error.StatusCode)
{
    StepText = step.Text  // decomposing the Step into a string
};
```

### DO: Keep the object reference

```csharp
// YES: store the Step, access .Text when you need it
public class StepError : Error
{
    public Step? Step { get; init; }

    public static StepError FromException(Exception ex, PLangContext context)
    {
        return new StepError(ex.Message, context)
        {
            Exception = ex,
            Step = context.Step  // keep the object
        };
    }
}
```

## Merge Belongs on the Result

When multiple operations produce results that need combining, the merge logic belongs on the result type — it knows its own structure.

```csharp
// Return knows how to merge with another Return
public Return Merge(Return other)
{
    if (other.Value == null) return this;
    // merge List<Data> by name, replace-or-append
    ...
}

// The collection uses it in a clean loop
public async Task<Return> RunAsync(Engine engine, PLangContext context, CancellationToken ct)
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
```

This follows the same principle: the object that owns the data owns the behavior on it.

## Don't Create Wrapper Objects for Data You Already Have

If the handler already receives everything it needs through an existing object, don't create a second object to pass the same data in a different shape.

### DON'T: Create intermediary context objects

```csharp
// NO: EventContext duplicates what PLangContext already has
var result = await Events.Before.RunAsync(new EventContext
{
    EventType = EventType.BeforeStep,
    GoalName = context.CurrentGoalName,   // already on context
    StepIndex = context.CurrentStepIndex, // already on context
    StepText = Text                       // already on context.Step
});
```

### DO: Pass the object that already has the data

```csharp
// YES: PLangContext already has Goal, Step, CurrentGoalName, CurrentStepIndex
var result = await Events.Before.Run(context);
```

## Summary

1. **Engine is the root** -- everything hangs off it
2. **Properties navigate** -- `engine.IO`, `engine.Goals`, `context.Events.Step.Before`
3. **Collections own their loops** -- `Steps.Load()`, not a foreach in Goal
4. **Methods belong on the object that owns the data** -- Goals loads goals, Steps loads steps
5. **Read like sentences** -- if it doesn't read naturally, it's on the wrong object
6. **Underlying objects get engine** -- any object that needs capabilities reaches them through engine
7. **No unnecessary DTOs** -- use real types inside the runtime
8. **Methods stay focused** -- one job per method
9. **Don't cache request state on shared objects** -- pass context as a parameter, don't store it
10. **Keep object references** -- store the Step, not step.Text; store the Goal, not goal.Name
11. **Merge belongs on the result** -- Return.Merge, not a loop in the caller
12. **Don't create wrapper objects** -- if the data is already on an existing object, pass that object
