# Object-Based Pattern (OBP)

OBP is an architectural pattern where objects own all logic for their data. No services, no managers, no middle layers.

## Core idea

Data flows without the caller knowing what's inside. Only the object that owns the data knows its shape — so all behavior lives on that object.

## Rules

1. **Root object** — One top-level object from which everything is reachable.

```csharp
// Navigate to what you need
app.goal.save(data);
engine.Channels.WriteTextAsync(StdOut, text);
engine.FileSystem.File.ReadAllTextAsync(path);
```

2. **Behavior on the owner** — Methods live on the object that owns the data. No services, no managers.

```csharp
// Wrong: GoalService saves goals
goalService.Save(goal, data);

// OBP: Goal saves itself
app.goal.save(data);
```

3. **Navigate, don't pass** — Don't thread parameters. Pass the root or the caller, let the callee navigate.

```csharp
// Wrong: decomposing into parameters
await path.Delete(p.Recursive, p.IgnoreIfNotFound);

// OBP: pass the caller, let it navigate
await path.Delete(p);
```

4. **Lazy everything** — Never load data until accessed. Zero friction.

```csharp
// Wrong: constructor loads everything
new Path(path) { _fileInfo = new FileInfo(path); }  // wasted CPU

// OBP: store only what's given, work on access
var path = new Path("/file.txt");  // stores a string
path.Size;                          // NOW it does the work
```

5. **Collections own their loops** — Parents delegate, never iterate.

```csharp
// Wrong: parent loops over children
foreach (var step in goal.Steps) await step.Load(context);

// OBP: collection owns the loop
await goal.Steps.Load(context);
```

6. **Keep references, not fields** — Store the object, not a copy of its properties.

```csharp
// Wrong: extracting a field
public class StepError { public string StepText { get; init; } }

// OBP: keep the reference
public class StepError { public Step? Step { get; init; } }
```

7. **Relay data, don't repackage** — Data flows through layers unchanged.

```csharp
// Wrong: unwrap and rewrap
return Data.Ok(result.Value);

// OBP: relay as-is
return result;
```

8. **No wrappers** — If the data exists on an object you already have, pass that object.

```csharp
// Wrong: copying fields into a new DTO
new EventContext { GoalName = context.CurrentGoalName };

// OBP: pass what you have
await Events.Before.Run(context);
```

## Why it matters

The object graph IS the architecture. An LLM (or human) can traverse `engine.Goals.Get("Start")` and know exactly where things live. No service layers to trace, no scattered logic to reconstruct. Less context needed, better results.

OBP is friction-free software. Nothing loads until needed. Nothing knows more than it has to. Nothing exists that doesn't earn its place. Every line of code does exactly what it's supposed to do — nothing more.
