# Data<T> Design — Composition Over Inheritance

## Problem

Data is the universal type in PLang. Currently, domain types (Path, Identity, Goal, Step, Action) inherit from Data directly. This conflates two concerns:

1. **Data's job**: variable container — Name, Type, Error, Properties, Context
2. **Domain object's job**: domain-specific properties and behavior (Path.Exists, Goal.Steps, Step.Text)

This creates several problems:

- **Thread safety**: Goal, Step, Action are loaded from `.pr` files and shared across threads. Per-execution state (Context, Error, Handled, Returned) lives on these shared objects via Data inheritance — a race condition.
- **Unwrapping confusion**: The system (Variables.Set, `__data__` rename, etc.) must inspect concrete types to decide whether to adopt or wrap Data subclasses. Different types get different treatment.
- **Coupled identity**: A Path can't exist without being a variable container. Every Path carries Name, Error, Properties, Signature overhead even when used transiently.

## Design Decision

**Composition over inheritance.** Domain objects are plain classes. Data<T> wraps them.

```
Before:  Path : Data          (Path IS a Data)
After:   Data<Path> wraps Path (Data HAS a Path)
```

The runtime handles one uniform type: `Data` (and `Data<T>`). It never knows or cares what's inside. Only the consumer — the handler that creates or uses the value — touches `.Value`.

## Two Patterns

### Value types (Path, Identity, etc.)

Created fresh per-execution by action handlers. Inherently thread safe.

```csharp
// Plain domain class — no Data inheritance
public class Path : IContextual
{
    private Context _context;

    public Path(string absolutePath, Context context)
    {
        _absolutePath = absolutePath;
        _context = context;
    }

    public Context Context { get; set; }
    private IPLangFileSystem Fs => _context.App.FileSystem;

    public string Absolute => _absolutePath;
    public bool Exists => Fs.File.Exists(_absolutePath);
    public string Extension => _context.App.FileSystem.Path.GetExtension(_absolutePath);
    // ...
}

// Handler creates and wraps:
public Task<Data> Run()
{
    var path = new Path(absolutePath, Context);
    return Data<Path>.Ok(path);
}
```

No cache needed. The handler creates it, wraps it in Data<T>, returns it. Done.

### Structural types (Goal, Step, Action)

Loaded from `.pr` files, shared across threads. Need per-execution Data wrappers with stable identity.

```csharp
// Plain domain class — structural template
public class Step : IDataWrappable
{
    public string Text { get; set; }
    public int Index { get; set; }
    public int Indent { get; set; }
    public List<Action> Actions { get; set; }
    // ... structural properties only

    // OBP: Step is responsible for its own Data representation
    public Data<Step> AsData(Context context)
    {
        return context.GetOrCreate(this, () =>
        {
            var data = new Data<Step>("", this);
            data.Context = context;
            return data;
        });
    }
}
```

Context provides a per-execution cache (dumb storage):

```csharp
// On Context:
private readonly Dictionary<object, Data.@this> _dataCache = new();

public Data<T> GetOrCreate<T>(T key, Func<Data<T>> factory) where T : class
{
    if (_dataCache.TryGetValue(key, out var existing))
        return (Data<T>)existing;

    var data = factory();
    _dataCache[key] = data;
    return data;
}
```

This ensures identity:

```csharp
// Engine executing a step:
var stepData = step.AsData(context);    // creates + caches

// PLang: %!goal.Steps[0]%
// Navigator finds Step, calls:
var stepData = step.AsData(context);    // hits cache -> same object

// Same reference. Always.
```

## Interfaces

### IContextual (existing pattern, renamed from IContext for domain objects)

Domain objects that need runtime access implement this. Data propagates Context changes to Value automatically.

```csharp
public interface IContextual
{
    Context Context { get; set; }
}
```

Used by: Path (needs FileSystem for Exists, Size — these are properties, not methods, so Context must be stored).
Not used by: Identity (pure data), Goal/Step/Action (shared templates — receive Context as method parameters instead, e.g., `RunAsync(context)`). Storing Context on shared structural types would break thread safety.

Note: The existing `IContext` interface on action handlers serves the same purpose for handlers. Whether to merge these or keep them separate is an implementation detail.

### IDataWrappable

Structural types that need cached per-execution Data wrappers.

```csharp
public interface IDataWrappable
{
    Data.@this AsData(Context context);
}
```

Used by: Goal, Step, Action (shared templates needing per-execution wrappers).
Not used by: Path, Identity (already per-execution, handlers wrap directly).

## Context Propagation

Data already propagates Context to its Type. Extended to propagate to Value:

```csharp
// Data.Context setter:
set
{
    _context = value;
    if (_type != null) _type.Context = value;
    if (_value is IContextual contextual)
        contextual.Context = value;
}
```

When a Data<Path> moves between actors or gets stored in a new context, the inner Path's Context updates automatically.

## Execution State

Properties like Handled, Returned, ReturnDepth, Disabled are execution-flow control, not variable/value properties. They move off Data:

| Property | Current Location | New Location | Rationale |
|----------|---------|--------|-----------|
| Returned | Data | Context | "This execution is returning" — flow control |
| ReturnDepth | Data | Context | "How many frames to unwind" — call stack concern |
| Handled | Data | Context / engine-local | "Error was resolved" — error handling flow |
| Disabled | Step (computed) | Context config lookup | "Skip this step" — configuration concern |
| Signature | Data | Stays on Data | About the value — travels with it |
| Error | Data | Stays on Data | Result semantics — "this operation failed" |

## Navigation

PLang variable access (`%path.Exists%`, `%!goal.Steps[0].Text%`) works through Data's existing Value navigation:

1. Variables.Get("path") returns Data<Path>
2. GetChild("Exists") navigates into Value (the Path object)
3. CLR reflection navigator finds Path.Exists
4. Returns Data("Exists", true)

For structural types in lists, the navigator uses IDataWrappable:

```csharp
// In list/collection navigator:
if (element is IDataWrappable wrappable && _context != null)
    return wrappable.AsData(_context);
else
    return new Data(key, element, parent: this);
```

## Thread Safety Summary

```
Shared (immutable after load):
  Goal  { Name, Steps, Description, Path }
  Step  { Text, Index, Indent, Actions }
  Action { Module, ActionName, Parameters }

Per-execution (via IDataWrappable + context cache):
  Data<Goal>   <- goal.AsData(context)
  Data<Step>   <- step.AsData(context)   <- same instance from engine AND navigation
  Data<Action> <- action.AsData(context)

Per-execution (created by handler):
  Data<Path>     <- handler returns Data<Path>.Ok(path)
  Data<Identity> <- handler returns Data<Identity>.Ok(identity)

Execution flow:
  context.Returned      <- on Context
  context.ReturnDepth   <- on Context
  Data.Error            <- on the wrapper (per-execution)
```

## The Discipline

The system only touches Data. Never the inner value. The boundary where `.Value` is accessed:

- **Creation**: handler creates the domain object, wraps in Data<T>
- **Consumption**: handler calls `data.GetValue<Path>()` to work with the domain object
- **PLang navigation**: transparent — Data navigates into Value via reflection/navigators

Everything in between — Variables, `__data__` rename, events, conditions, channels — only sees Data.

## Implementation Phases

### Phase 1: Value types (Path, Identity)

Smallest blast radius, proves the pattern:
- Make Path a plain class (remove Data inheritance), implement IContextual
- Make Identity a plain class (no interfaces needed — pure data)
- Update handlers to return Data<T>
- Update navigation to handle Value traversal (already works for CLR objects)
- Add Context propagation to Value in Data.Context setter

### Phase 2: Structural types (Goal, Step, Action)

- Make Goal, Step, Action plain classes (remove Data inheritance)
- Implement IDataWrappable on each
- Add context cache (GetOrCreate) to Context
- Update navigator to use IDataWrappable for list elements
- Move Returned, ReturnDepth, Handled to Context
- Update execution engine to check Context for flow control

### Phase 3: Clean up Data

- Remove subclass-specific reflection logic from GetChildValue
- Remove Variables.Set type-checking logic (adopt vs wrap)
- Simplify Clone, serialization
- Remove any dead code from the inheritance era
