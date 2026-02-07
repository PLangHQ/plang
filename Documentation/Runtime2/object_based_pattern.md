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
    _context = context;
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

## Summary

1. **Engine is the root** -- everything hangs off it
2. **Properties navigate** -- `engine.IO`, `engine.Goals`, `context.Events.Step.Before`
3. **Collections own their loops** -- `Steps.Load()`, not a foreach in Goal
4. **Methods belong on the object that owns the data** -- Goals loads goals, Steps loads steps
5. **Read like sentences** -- if it doesn't read naturally, it's on the wrong object
6. **Underlying objects get engine** -- any object that needs capabilities reaches them through engine
7. **No unnecessary DTOs** -- use real types inside the runtime
8. **Methods stay focused** -- one job per method
