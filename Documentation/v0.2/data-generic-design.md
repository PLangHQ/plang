# Data<T> Design — Composition Over Inheritance

## Guiding Principle

**Get to strongly typed, specific objects as soon as possible.**

The builder knows the action schema. It stamps correct types at build time. The runtime loads typed Data from .pr files. Handlers access `.Value` only at the boundary where actual work happens. Data is never unwrapped in the runtime pipeline.

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

---

## The Full Pipeline

### 1. PLang developer writes code

```
ReadConfig
- read file 'config.json', write to %config%
- set %appName% to %config.name%
```

### 2. LLM returns plain values

The LLM's output IS Data — each parameter has name, value, type — it just doesn't know that's what it is. The LLM prompt formalizes this: "Each parameter is a Data object with name, value, and type."

```json
{
  "actions": [{
    "module": "file",
    "action": "read",
    "parameters": [
      { "name": "Path", "value": "config.json", "type": "string" }
    ]
  }]
}
```

The LLM guesses `"type": "string"` because it sees a string literal. That's fine — the builder corrects it.

### 3. Builder validates and formalizes

**The default `IBuilder.Validate()`** (`app/modules/builder/code/Default.cs`) reflects on the `file.read` action class:

```csharp
public partial class read : IContext
{
    public partial Data<Path> Path { get; init; }
    public partial Data<string> Encoding { get; init; }
}
```

The builder sees: parameter "Path" maps to property `Data<Path>`. The inner type is `Path`. It corrects the LLM's type:

```json
{ "name": "Path", "value": "config.json", "type": "path" }
```

**Type inference across actions**: The builder knows `file.read` returns `Data<Path>`. When the next action (`variable.set`) uses `%__data__%`, the builder infers the type from the previous action's return type:

```json
// Before (blind):
{ "name": "Value", "value": "%__data__%", "type": "object" }

// After (builder infers from file.read's return type):
{ "name": "Value", "value": "%__data__%", "type": "path" }
```

The builder is the formalization layer. It has the action schema. It has visibility of the action chain. It stamps correct types — strongly typed as soon as possible.

### 4. Saves .pr file

```json
{
  "name": "ReadConfig",
  "steps": [{
    "text": "read file 'config.json', write to %config%",
    "index": 0,
    "actions": [{
      "module": "file",
      "action": "read",
      "parameters": [
        { "name": "Path", "value": "config.json", "type": "path" }
      ]
    }, {
      "module": "variable",
      "action": "set",
      "parameters": [
        { "name": "Name", "value": "config", "type": "string" },
        { "name": "Value", "value": "%__data__%", "type": "path" }
      ]
    }]
  }]
}
```

Types are correct on disk. Values are still raw (string literals, `%var%` references). The type tells the runtime what to convert them into.

### 5. Runtime loads .pr file

`Goals.LoadFromFileAsync()` deserializes JSON into Goal, Steps, Actions. Each parameter becomes:

```
Data { Name="Path", Value="config.json", Type="path" }
```

Data.Type is already correct from the .pr file. No guessing needed.

### 6. Source generator's ExecuteAsync

Handler properties are `Data<T>`. Resolution uses null check — no separate `_set` flag needed because Data is never null:

```csharp
public partial class read : ICodeGenerated
{
    private Data<Path>? __Path_backing;

    public partial Data<Path> Path
    {
        get
        {
            if (__Path_backing == null)
                __Path_backing = __Resolve<Path>("path");
            return __Path_backing!;
        }
    }

    public async Task<Data.@this> ExecuteAsync(Action action, Context context)
    {
        __action = action;
        __variables = context.Variables;
        Context = context;

        if (Path == null) return Error(...);

        return await Run();
    }
}
```

### 7. Lazy resolution — Data converts itself

`__Resolve` finds the parameter Data and asks it to convert itself. OBP — Data owns the conversion because it has the Value, the Type, and the Context:

```csharp
private Data<T> __Resolve<T>(string name)
{
    var data = FindParameter(name);

    if (data.Value is string str && str.Contains('%'))
    {
        var resolved = ResolveVariables(str);
        return resolved.As<T>(Context);
    }

    return data.As<T>(Context);
}
```

```csharp
// On Data:
public Data<T> As<T>(Context context)
{
    if (Value is T already)
        return new Data<T>(Name, already, Type);

    // Data owns the conversion, using its Type knowledge
    var converted = Type.Convert(Value, typeof(T), context);
    return new Data<T>(Name, (T)converted, Type);
}
```

For `Data { Value="config.json", Type="path" }`:
- Type says "path" → convert string to Path via `Path.Resolve("config.json", context)`
- Returns `Data<Path> { Value=Path(...), Type="path" }`

For `Data { Value="%myPath%", Type="path" }`:
- Resolves `%myPath%` from Variables → might already be `Data<Path>`
- If already correct type → pass through, no conversion
- If wrong type → `data.As<Path>(context)` converts

### 8. Handler runs

```csharp
public Task<Data.@this> Run()
{
    // Path is Data<Path> — unwrap at the boundary where actual work happens
    var path = Path.Value;
    var content = await Fs.ReadAllTextAsync(path.Absolute);

    return Data<string>.Ok(content);
}
```

`.Value` accessed here — the lowest level, where the actual filesystem call happens. Nowhere before this.

### 9. Result flows back

```csharp
// Action.RunAsync:
var result = await context.App.Run(this, context);
result.Name = "__data__";
context.Variables.Put(result);   // stored as %__data__%
```

The runtime only touches Data. It renames `.Name`, stores it. Never looks at `.Value`.

### 10. Next action picks up typed Data

`variable.set` with `Value = "%__data__%"` and `Type = "path"`:
- `context.Variables.Get("__data__")` returns the `Data<Path>` from the previous action
- Already the correct type → passes through, no conversion
- Stored as `%config%` — still `Data<Path>`

### 11. Dot-path navigation

Next step: `%config.name%`
- `Variables.Get("config.name")` → gets root `Data<Path>` for "config"
- `Data.GetChild("name")` → navigates into Value via CLR reflection/navigators
- Returns `Data { Name="name", Value="MyApp" }`

---

## Two Patterns for Domain Objects

### Value types (Path, Identity, etc.)

Created fresh per-execution by action handlers. Inherently thread safe. No cache needed.

```csharp
// Plain domain class — no Data inheritance
public class Path : IContext
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
}

// Handler creates and wraps:
public Task<Data> Run()
{
    var path = new Path(absolutePath, Context);
    return Data<Path>.Ok(path);
}
```

### Structural types (Goal, Step, Action)

Loaded from `.pr` files, shared across threads. Need per-execution Data wrappers with stable identity. The domain object owns its Data representation (OBP):

```csharp
public class Step : IDataWrappable
{
    public string Text { get; set; }
    public int Index { get; set; }
    public int Indent { get; set; }
    public List<Action> Actions { get; set; }

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

Identity guaranteed:

```csharp
// Engine executing a step:
var stepData = step.AsData(context);    // creates + caches

// PLang: %!goal.Steps[0]%
// Navigator finds Step, calls:
var stepData = step.AsData(context);    // hits cache -> same object

// Same reference. Always.
```

---

## Interfaces

### IContext (existing interface)

Domain objects that need runtime access implement `IContext` — the same interface action handlers already use. Data propagates Context changes to Value automatically.

```csharp
public interface IContext
{
    Context Context { get; set; }
}
```

Used by: Path (needs FileSystem for Exists, Size — properties, not methods, so Context must be stored), action handlers (need Context to execute).
Not used by: Identity (pure data), Goal/Step/Action (shared templates — receive Context as method parameters instead, e.g., `RunAsync(context)`). Storing Context on shared structural types would break thread safety.

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

---

## Context Propagation

Data already propagates Context to its Type. Extended to propagate to Value:

```csharp
// Data.Context setter:
set
{
    _context = value;
    if (_type != null) _type.Context = value;
    if (_value is IContext contextual)
        contextual.Context = value;
}
```

When a Data<Path> moves between actors or gets stored in a new context, the inner Path's Context updates automatically.

---

## Data Owns Its Conversion

OBP: Data has the Value, the Type, and access to Context. It converts itself:

```csharp
// On Data:
public Data<T> As<T>(Context context)
{
    if (Value is T already)
        return new Data<T>(Name, already, Type);

    var converted = Type.Convert(Value, typeof(T), context);
    return new Data<T>(Name, (T)converted, Type);
}
```

No external TypeMapping calls from the source generator. Data does its own conversion.

---

## Action Handler Properties Are Data<T>

Every property on an action record is `Data<T>`, not a primitive:

```csharp
[Action("read")]
public partial class read : IContext
{
    public partial Data<Path> Path { get; init; }
    public partial Data<string> Encoding { get; init; }

    public Task<Data.@this> Run()
    {
        var content = await Fs.ReadAllTextAsync(Path.Value.Absolute);
        return Data<string>.Ok(content);
    }
}
```

The source generator resolves `%var%` references into `Data<T>`. The handler accesses `.Value` at the boundary where it does actual work.

Null check for lazy resolution uses `== null` — no separate `_set` flag needed because Data is never null.

---

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

---

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

---

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

---

## The Complete Pipeline Summary

```
PLang code     "read file 'config.json'"         human intent
    |
LLM            { value: "config.json", type: "string" }  casual guess
    |
Builder        { value: "config.json", type: "path" }    formalized, correct type
    |                                                     (infers __data__ types from
    |                                                      previous action return types)
    |
.pr file       Data { Value="config.json", Type="path" } strongly typed on disk
    |
Runtime load   Data { Value="config.json", Type="path" } deserialized, type intact
    |
__Resolve      data.As<Path>(context)                     Data converts itself
    |
Handler prop   Data<Path>                                 Data wrapper, not raw Path
    |
Handler.Run()  Path.Value.Absolute                        .Value at the boundary
    |
Return         Data<string>.Ok(content)                   wrapped result
    |
Runtime        Data flows, renamed, stored                never unwrapped
    |
Next handler   data.As<T>(context)                        Data converts itself again
```

**Data is never unwrapped in the runtime. Only at handler boundaries — where the actual work happens.**

---

## Implementation Phases

### Phase 1: Value types (Path, Identity)

Smallest blast radius, proves the pattern:
- Make Path a plain class (remove Data inheritance), implement IContext
- Make Identity a plain class (no interfaces needed — pure data)
- Update handlers to return Data<T>
- Add `Data.As<T>(context)` conversion method
- Update navigation to handle Value traversal (already works for CLR objects)
- Add Context propagation to Value in Data.Context setter

### Phase 2: Structural types (Goal, Step, Action)

- Make Goal, Step, Action plain classes (remove Data inheritance)
- Implement IDataWrappable on each
- Add context cache (GetOrCreate) to Context
- Update navigator to use IDataWrappable for list elements
- Move Returned, ReturnDepth, Handled to Context
- Update execution engine to check Context for flow control

### Phase 3: Action handler properties as Data<T>

- Update action records: all properties become Data<T>
- Update source generator: __Resolve returns Data<T>, null check replaces _set flag
- Update builder: stamp correct types from action schema, infer __data__ types from action chain
- Formalize Data concept in LLM prompt ("each parameter is a Data with name, value, type")

### Phase 4: Clean up

- Remove subclass-specific reflection logic from GetChildValue
- Remove Variables.Set type-checking logic (adopt vs wrap)
- Remove TypeMapping calls from source generator (Data.As<T> handles conversion)
- Simplify Clone, serialization
- Remove any dead code from the inheritance era

---

## Resolution semantics (`Data.As<T>`) — cycle, depth, error contract

The simplified `As<T>` shown in section 7 above is the design sketch. The shipped implementation in `app/data/this.cs` adds three guards plus a `ServiceError` contract; both halves matter for handler correctness.

### Cycle protection

A `[ThreadStatic] HashSet<string>? _resolvingValues` tracks the raw `%var%`-containing strings currently being resolved on the current thread. When `As<T>` recurses into a string already in the set (e.g. `%a%="%b%", %b%="%a%"`), it returns `Data<T>.FromError(new ServiceError(..., "VariableResolutionCycle", 400))` instead of overflowing the stack.

### Depth bound

The HashSet alone misses *expanding* cycles where each recursion level produces a new string (e.g. `%a%="X-%b%", %b%="Y-%a%"` → `"X-Y-X-Y-..."`). For those, `ResolveDepthLimit = 32` caps recursion. Real handler chains run 1–5 levels; the limit is well above any legitimate use. Trip → `Data<T>.FromError(new ServiceError(..., "ResolveDepthExceeded", 400))`.

### Action-destination carve-out

When `T` is `Action.@this` (or `IEnumerable<Action.@this>`), sub-actions hold raw `%var%` strings for *deferred* resolution at their own dispatch time. `As<T>` skips the variable walk entirely and converts the raw value straight through `TypeMapping`. Without this carve-out, dispatching an outer step would prematurely resolve everything inside its sub-actions.

### Error capture in generated handlers

Handlers declare `Data<T>` properties. The source-generated getter assigns `__ResolveData(name).As<T>(Context)` to a backing field — but when `As<T>` returns `FromError(...)` (cycle or depth-trip), the FromError-Data lives silently on the backing field with `Value = default(T)`. A `Run()` body that reads `.Value` would proceed with a default value, masking the resolution error.

The fix is two-part. The generator emits both halves:

```csharp
// In each Data<T> property getter:
get {
    if (__Body_backing == null) {
        __Body_backing = __ResolveData("body").As<string>(Context);
        if (!__Body_backing.Success) __resolutionError = __Body_backing;
        __Body_set = true;
    }
    return __Body_backing!;
}

// In ExecuteAsync, AFTER Run() completes:
if (__resolutionError != null) return __resolutionError;
var __runResult = await Run();
if (__resolutionError != null) return __resolutionError;
return __runResult;
```

The pre-Run check catches eager-validated raw scalars that fail; the post-Run check catches Data<T> getters that fired during `Run()` (the common case). Both checks are load-bearing — removing either re-introduces the silent-default bug.

### Why .Value is raw

Section 7's sketch shows `.Value` triggering substitution. The shipped contract makes `.Value` the *raw* stored value — no `%var%` substitution, no caching. Each `As<T>(context)` call resolves freshly against the current variable store. This means:

- A `Data<string>` carrying `"%user.name%"` reports `.Value == "%user.name%"` until `As<string>(context)` is called.
- Variables set later in the goal are visible — there is nothing stale to invalidate.
- Resolution caching, if any, lives on the caller (e.g. the source-generated backing field).

---

## Identity preservation — `As<T>` wrap rules + `AsCanonical`

Section 7's `As<T>` sketch always allocates a fresh `Data<T>` and copies `.Value`. The shipped contract preserves identity instead: a typed view of a variable shares state with the underlying binding so `Properties` mutations and event subscribers are visible through every alias. This is what makes `--debug={"variables":[...]}` survive re-bindings and what lets handlers attach metadata to a variable that downstream readers can see.

The principle: **every plang variable IS `Data`.** A `Data<T>` is a typed *view* of a variable, not a copy of it. The "canonical" Data — the live variable when the slot resolves a `%var%`, the parameter Data when the slot is literal — owns the state; views alias it.

### The four `As<T>` rules

`Data.As<T>(context)` (in `app/data/this.cs`, `WrapAs<T>`) decides between four cases based on the canonical's runtime shape:

| Case | Source | Target | Outcome |
|---|---|---|---|
| **1. Same-type fast path** | `this is Data<T>` and `.Value is T` | `Data<T>` | returns `this`. No allocation. |
| **2. Variance fast path** | `value is T` and `IsPlangAssignable(T, value.GetType())` | `Data<T>` | new `Data<T>`; `.Value` is the same reference (cast-only); `Properties`, `OnCreate`, `OnChange`, `OnDelete` aliased from `this`. |
| **3. Cross-type with conversion** | `value` can't satisfy `T` as-is | `Data<T>` | new `Data<T>` with converted `.Value`; the four state slots aliased from `this`. `T == IEnumerable` delegates to `Data.AsEnumerable()` so the string-not-iterable rule has one source. |
| **4. Conversion failure** | conversion errored | `Data<T>.FromError(error)` | sentinel; nothing aliased. The post-Run resolution check (above) surfaces it. |

Aliasing means `Properties` / `OnCreate` / `OnChange` / `OnDelete` are list references shared between source and view. `wrapped.Properties.Set("note", x)` is visible through `source.Properties`. A handler subscribing to `wrapped.OnChange` fires when `Variables.Set` replaces the underlying binding. Subscribers added at any point — to source, to any view, before or after replacement — are visible from every alias because they share the same list ref.

### `AsCanonical` — plain `Data` slots bypass `As<T>` entirely

When a handler property is plain `Data` (untyped), the source generator emits `__ResolveData(name).AsCanonical(Context)` instead of `As<object>`. `AsCanonical`:

- **Full match `%var%`** → returns the LIVE variable Data from `Variables.Get(name)`. Mutating `.Value` on the result IS mutating the variable. This is what `list.add` relies on: it reads `List.Value as List<object?>`, calls `.Add(...)`, and the live variable sees the change without any `Variables.Set` write-back.
- **Literal value (no `%`)** → returns `this` (the parameter Data) — same ref.
- **Partial interpolation `"hello %x%"`** → returns a transient `Data` with the interpolated string and the *slot* Name (a partial isn't a reference to any single variable). State aliased from `this`.
- **Container with nested `%var%`** (list / dict) → walked via the shared `WalkContainerVars` helper; returns a transient `Data` over a fresh container with substituted values. State aliased from `this`.
- **Unset `%var%`** → returns a not-initialized `Data` with the variable's name (so handler diagnostics see "missing %x%", not "missing slot").

The walker is shared between `AsCanonical` (plain Data) and `AsT_Impl` (typed Data) so nested variables resolve by one rule on both paths. A drift here was the bug coder/v2 fixed: `AsCanonical` returned containers unchanged while typed Data walked them, so `set ... type=json` over a list-of-dicts saw literal `"%var%"` strings inside the parameter.

### `Variables.Set` — events follow the name, Properties stay with the Data

Identity preservation continues at the storage layer. When `Variables.Set(dv)` replaces an existing binding under the same name:

```csharp
// PLang/app/variables/this.cs
if (_variables.TryGetValue(name, out var prev) && !ReferenceEquals(prev, dv))
{
    dv.OnCreate = prev.OnCreate;   // alias — same list refs
    dv.OnChange = prev.OnChange;
    dv.OnDelete = prev.OnDelete;
    prev.FireOnChange(dv);
}
```

Each `Data` under a name shares the *same* event-list refs as every prior binding. New subscribers added at any point are visible to all subsequent re-bindings, so debug watches survive any number of replacements. **Properties don't carry across replacement** — they're metadata about the *value* (e.g. `condition.if`'s `branchIndex` lives in `Properties` of that step's `__data__` Data; replacing `__data__` on the next step shouldn't bleed through stale metadata). Events follow the *name*, Properties stay with the *Data instance*.

`variable.set` is the **sole binding-mint site** for user-visible variables. Its `MintTyped` if-chain switches on the runtime type of the bound value (`string`/`int`/`long`/`bool`/`Guid`/`byte[]`/`List`/`Dict`/...) and constructs the right `Data<T>`; mutable refs (List, Dict) are snapshot-cloned via JSON roundtrip so later `set %x.field% = ...` against the source doesn't bleed through. Cold types fall through to a reflection construction (`typeof(Data<>).MakeGenericType`).

### `IsPlangIterable` / `IsPlangAssignable` — strings are atomic

C# treats `string` as `IEnumerable<char>`. Plang treats strings as atomic. The carve-out lives in two helpers used by `As<T>`, `AsEnumerable`, and `EnumerateItems`:

```csharp
internal static bool IsPlangIterable(object? value) =>
    value is IEnumerable && value is not string;

internal static bool IsPlangAssignable(Type target, Type source) {
    if (typeof(IEnumerable).IsAssignableFrom(target) && source == typeof(string))
        return false;
    return target.IsAssignableFrom(source);
}
```

Without `IsPlangAssignable`, Rule 2 (variance fast path) would treat `Data<string>` as variance-assignable to `Data<IEnumerable>`, letting `foreach %s%` over `s = "hello"` iterate five chars instead of running once with the string itself. With the carve-out, a `Data<string>` resolved to `Data<IEnumerable>` falls into Rule 3, which delegates to `AsEnumerable` and produces `["hello"]` — a one-element sequence.
