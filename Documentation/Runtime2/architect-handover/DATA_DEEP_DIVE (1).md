# Data Deep Dive — The Universal Type in PLang Runtime2

This document traces `Data` through every touchpoint in Runtime2: how it's created, stored, navigated, passed as parameters, returned as results, serialized, cached, and used in error handling. `Data` is the single most important type in Runtime2 — it replaced both the old variable system and the old `Return` type.

---

## 1. What Data Is

Data serves three roles simultaneously:

1. **Variable wrapper** — every variable in MemoryStack is a `Data` instance (`%name%` → `Data { Name="name", Value="Ingi" }`)
2. **Result type** — every method in the execution chain returns `Task<Data>` with Success/Error semantics
3. **Parameter carrier** — action parameters arrive as `List<Data>` with name-value pairs

This unification means the same object flows through the entire system with no type conversions between roles.

### The Data class layout

```
Data
│
│  Identity & Hierarchy
├── Name: string              Stripped of % delimiters. Case-insensitive lookup.
├── Path: string              Hierarchical: "user.address.city" or "items[0].name"
├── Parent: Data?             For navigation chains (GetChild creates parent→child links)
│
│  Value & Type
├── Value: virtual object?    The actual payload. Setter auto-unwraps JsonElement/JToken.
├── Type: Type?               PLang type descriptor ("string", "int", "image/jpeg", etc.)
├── IsInitialized: bool       True once Value has been set (even to null via setter)
├── IsEmpty: bool             !IsInitialized || null || empty string
│
│  Timestamps
├── Created: DateTime         Set once in constructor
├── Updated: DateTime         Refreshed on every Value setter call
│
│  Metadata
├── Properties: Properties    IList<Data> with named access. Extensible child properties.
│
│  Result semantics
├── Error: IError?            Non-null = failure
├── Success: bool             Error == null
├── Handled: bool             Set by before-events to short-circuit execution
├── Warnings: List<Info>?
│
│  Operators
├── implicit operator bool    Returns Success — enables `if (!result)` pattern
│
│  Navigation
├── GetValue<T>()             Cast or convert via TypeMapping.ConvertTo
├── GetValue(Type)            Non-generic version
├── GetChild(path)            Dot/bracket navigation, recursive
│
│  Static factories
├── Ok()                      Success with no value (Name="")
├── Ok(value, type?)          Success with value
├── FromError(IError)         Failure
├── Null(name)                Uninitialized named Data
│
│  Merging
└── Merge(other)              Combines List<Data> by Name (replace-or-append)
```

### Subclasses

| Class | Purpose | Key Difference |
|-------|---------|----------------|
| `Data<T>` | Strongly-typed generic | `new T? Value` property shadows base, typed `Ok(T)` and `FromError` |
| `DynamicData` | Computed values | `override object? Value => _valueFactory()` — value computed on every access |

---

## 2. Data Construction & Value Unwrapping

### Constructor chain

```csharp
new Data(string name, object? value = null, Type? type = null, Data? parent = null)
```

1. **Name cleaning**: `CleanName(name)` strips whitespace and `%` delimiters
2. **Value unwrapping**: `UnwrapJsonElement(value)` normalizes the value
3. **Type inference**: If no type provided and value is non-null, auto-derives via `TypeMapping.GetTypeName(value.GetType())`
4. **Path building**: `BuildPath(parent, name)` creates hierarchical path (`parent.name` or `parent[0]`)
5. **Timestamps**: Created = Updated = UtcNow

### Value Unwrapping (the normalization layer)

Every time a value enters Data (constructor or setter), `UnwrapJsonElement` runs:

```
Input                           → Output
─────────────────────────────────────────────────────
JsonElement (String)            → string
JsonElement (Number)            → long (if fits) or double
JsonElement (True/False)        → bool
JsonElement (Null/Undefined)    → null
JsonElement (Object)            → Dictionary<string, object?> (case-insensitive, recursive)
JsonElement (Array)             → List<object?> (recursive)
Newtonsoft JValue               → underlying CLR value (via reflection, no import)
Newtonsoft JObject/JArray       → round-trip: ToString() → JsonDocument.Parse → recurse
Everything else                 → passed through as-is
```

This means **Data never stores raw JsonElement or JToken**. Values are always plain CLR types. This is critical for the navigator system and type conversion.

### Value setter behavior

```csharp
set {
    _value = UnwrapJsonElement(value);   // normalize
    Updated = DateTime.UtcNow;           // track change time
    IsInitialized = true;                // mark as set
    _type = _value != null               // re-derive type
        ? new Type(TypeMapping.GetTypeName(_value.GetType()))
        : null;
}
```

The type is **always re-derived from the actual CLR type** on set. This means if you set a string value on Data that was previously int, the Type changes to "string" automatically.

---

## 3. Type System

### Type class

`Type` is a simple wrapper around a string value. It bridges PLang's type system to .NET:

```
Type("string")  → ClrType = typeof(string)
Type("int")     → ClrType = typeof(int)
Type("image/jpeg") → ClrType = typeof(byte[])   // MIME type resolution
Type("list<string>") → ClrType = typeof(List<string>)  // generic syntax
Type("actor")   → ClrType = typeof(Actor)
Type("goal.call") → ClrType = typeof(GoalCall)
```

### TypeMapping — the conversion engine

`TypeMapping` is the central type bridge. Key capabilities:

**Name → CLR Type** (`GetType`):
- Primitives: string, text, int, integer, long, float, double, decimal, bool, boolean, datetime, guid, bytes
- Collections: list, array, dictionary, dict, map, object, dynamic, json
- Runtime types: actor, goal.call, tstring
- Generics: `list<string>`, `dict<string,int>` (parsed at runtime)
- MIME types: `text/*` → string, `image/*`/`audio/*`/`video/*` → byte[], `application/json` → object
- Nullable: `int?`, `long?`, `bool?`, etc.

**CLR Type → Name** (`GetTypeName`):
- Reverse mapping for display and serialization
- Generic awareness: `List<string>` → `"list<string>"`
- Array coercion: `byte[]` → `"bytes"`, other arrays → `"list<elementType>"`
- ValidValues convention: types with `static string[] ValidValues` get lowercased name

**Value conversion** (`ConvertTo`):
- Nullable unwrapping
- Enum parsing (string → enum, enum → enum)
- GoalCall conversion (string → GoalCall, Dictionary → GoalCall, JsonElement → GoalCall)
- Primitive conversion via `Convert.ChangeType`
- Assignability check (skip conversion if already compatible)

### TypeJsonConverter & PlangTypeConverter

Two converters ensure `Type` serializes cleanly:
- `TypeJsonConverter`: System.Text.Json — writes/reads `Type` as a plain JSON string `"string"`
- `PlangTypeConverter`: .NET TypeConverter — enables Newtonsoft auto-discovery via `[TypeConverter]` attribute. No Newtonsoft dependency needed.

---

## 4. Data in MemoryStack

### Storage

```
MemoryStack._variables: ConcurrentDictionary<string, Data>
                        (case-insensitive keys)
```

Every variable in PLang is a `Data` entry in this dictionary. The MemoryStack never stores raw values — always `Data` wrappers.

### Writing Data into MemoryStack

**`Put(Data)`** — stores the Data instance directly (used for system/context variables):
```csharp
_variables[value.Name] = value;
```

**`Set(name, value, type?)`** — creates or updates:
```csharp
name = CleanName(name);       // strip %, trim
if (existing found) {
    existing.Value = value;    // triggers unwrap + type re-derive
    if (type != null) existing.Type = type;
} else {
    _variables[name] = new Data(name, value, type);
}
```

Key detail: `Set` **reuses existing Data instances**. The same Data object stays in the dictionary, only its Value changes. This matters because anything holding a reference to that Data sees the update.

### Reading Data from MemoryStack

**`Get(name)`** → `Data?`:
1. Clean name (strip %, trim)
2. If name contains `[`, resolve variable references in brackets: `addresses[idx]` → `addresses[1]` (looks up `idx` in the stack)
3. Extract root name (before first `.` or `[`)
4. Look up root in dictionary
5. If path has remaining segments → `root.GetChild(remaining)` (navigation)

**`GetValue(name)`** → `object?`: shorthand for `Get(name)?.Value`

**`Get<T>(name)`** → `T?`: shorthand for `Get(name)?.GetValue<T>()`

### System variables (registered in constructor)

```csharp
Put(new DynamicData("Now",    () => DateTime.Now,      Type.DateTime));
Put(new DynamicData("NowUtc", () => DateTime.UtcNow,   Type.DateTime));
Put(new DynamicData("GUID",   () => Guid.NewGuid(),    Type.FromName("guid")));
```

These are `DynamicData` — value computed fresh on every access.

### Context variables (registered by PLangContext)

```csharp
ms.Put(new Data("!engine", Engine));
ms.Put(new Data("!context", this));
ms.Put(new Data("!memoryStack", ms));
ms.Put(new Data("!fileSystem", Engine.FileSystem));
ms.Put(new DynamicData("!callStack", () => CallStack));
ms.Put(new Data("!channels", Engine.Channels));
ms.Put(new Data("!serializers", Engine.Serializers));
ms.Put(new DynamicData("!goal", () => Goal));    // changes during execution
ms.Put(new DynamicData("!step", () => Step));    // changes during execution
```

The `!` prefix is a convention — these are excluded from `GetNames()` and `GetAll()` but accessible via `Get("!engine")`.

### Clone behavior

```csharp
MemoryStack.Clone() {
    // Deep-clones all non-system, non-! variables
    // Uses Force.DeepCloner for value deep copy
    // System vars (Now, NowUtc, GUID) recreated fresh
    // ! context vars NOT cloned (they're context-specific)
}
```

Used when creating child contexts (`PLangContext.CreateChild()`).

---

## 5. Data Navigation (GetChild + Navigators)

When you access `%user.address.city%`, the system:

1. MemoryStack.Get("user.address.city")
2. Root = "user", Remaining = "address.city"
3. Look up `user` Data in dictionary
4. Call `userData.GetChild("address.city")`

### GetChild algorithm

```
GetChild("address.city"):
1. Parse path: segment = "address", remaining = "city"
2. GetChildValue("address") → uses ValueNavigators
3. Wrap result: new Data("address", childValue, parent: this)
4. Recurse: child.GetChild("city")
```

Path parsing handles three forms:
- Dot notation: `address.city` → segment="address", remaining="city"
- Bracket notation: `[0].value` → segment="0", remaining="value"
- Mixed: `items[0].name` → segment="items", remaining="[0].name"

### Navigator chain (priority order)

```
ValueNavigators._navigators = [
    DictionaryNavigator,    // IDictionary<string,object?> or IDictionary
    ListNavigator,          // IList
    JsonStringNavigator,    // string that looks like JSON
    ObjectNavigator,        // any object (reflection fallback)
]
```

First navigator that returns `CanNavigate(value) == true` handles the lookup.

#### DictionaryNavigator
- Handles: `IDictionary<string, object?>` (case-insensitive key scan), `IDictionary`
- This is the most common path because JSON objects unwrap to `Dictionary<string, object?>`

#### ListNavigator
- Handles: `IList`
- **Numeric key**: `"0"` → `list[0]`
- **Named accessors**: `"first"` → `list[0]`, `"last"` → `list[^1]`, `"random"` → random element, `"count"`/`"length"` → `list.Count`
- **Implicit first element delegation**: any other key → `ValueNavigators.Navigate(list[0], key)`. This means `%addresses.street%` automatically navigates through the first element: `addresses[0].street`

#### JsonStringNavigator
- Handles: strings starting with `{` or `[`
- Parses the JSON string into CLR types (same unwrap logic as Data constructor)
- Then delegates to `ValueNavigators.Navigate` on the parsed object
- This means a string-valued variable containing JSON is automatically navigable

#### ObjectNavigator
- Handles: anything (always returns `CanNavigate = true`)
- Uses reflection: `value.GetType().GetProperty(key, IgnoreCase)?.GetValue(value)`
- This is the fallback that handles typed C# objects (records, classes)

### Navigation creates Data chains

Each `GetChild` call creates a new `Data` with a parent link:

```
userData (root, no parent)
  └── addressData (parent = userData, path = "user.address")
       └── cityData (parent = addressData, path = "user.address.city")
```

The Path is built as: `parent.Path + "." + name` (or `parent.Path + "[" + name + "]"` for numeric names).

**These child Data instances are ephemeral** — created on demand, not cached. Each navigation creates fresh wrappers.

---

## 6. Data as Action Parameters

### How parameters flow from .pr file to action handler

1. **Stored in .pr file**: Action has `Parameters: List<Data>` — each Data has Name and Value (may contain `%var%` references)

2. **Deserialized**: JSON deserializer creates `Data` instances via the `[JsonConstructor]` constructor. Values go through `UnwrapJsonElement`.

3. **Source-generated CodeGeneratedExecuteAsync**: The generated code resolves each parameter:
   - Finds matching Data by name from the `List<Data>` 
   - If value is a string containing `%var%`, resolves variables from MemoryStack
   - Converts to the target CLR type via `TypeMapping.ConvertTo`
   - Sets the partial property on the action handler

4. **Action.RunAsync orchestration**:
   ```csharp
   // Libraries resolves the handler
   var (handler, error) = engine.Libraries.GetCodeGenerated(Module, ActionName, context);
   
   // Generated code receives the raw List<Data>
   result = await handler.CodeGeneratedExecuteAsync(Parameters, engine, context);
   
   // Return variables written to MemoryStack
   if (result.Value != null && this.Return != null) {
       foreach (var returnVar in this.Return)
           context.MemoryStack.Set(returnVar.Name, result.Value, result.Type);
   }
   ```

### Parameter resolution by generated code

The source generator creates code like (conceptual):

```csharp
public async Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context) {
    Context = context;
    
    // Find parameter by name, resolve %var% in string values
    var nameParam = parameters.Find(p => p.Name == "name");
    Name = ResolveAndConvert<string>(nameParam, context.MemoryStack);
    
    var valueParam = parameters.Find(p => p.Name == "value");
    Value = ResolveAndConvert<object>(valueParam, context.MemoryStack);
    
    return await Run();
}
```

The `[VariableName]` attribute tells the generator to strip `%` from the resolved value (it's a variable name, not a value).

The `[Default(value)]` attribute provides a fallback if the parameter is missing or null.

---

## 7. Data as Results (Return Flow)

### Action → Step → Goal → Engine

Every method returns `Task<Data>`. The chain:

```
Action.RunAsync returns Data
  → if Return vars defined: result.Value written to MemoryStack
  → Data propagates up

StepActions.RunAsync:
  → runs actions sequentially
  → merges results: merged = merged.Merge(result)
  → Merge combines List<Data> by Name (replace-or-append)

Step.RunAsync:
  → receives merged action result
  → if error + OnError: HandleErrorAsync
  → propagates result up

Goal.RunAsync:
  → iterates steps
  → if step fails and !IgnoreError: returns error immediately
  → on success: returns Data.Ok()

Engine.RunGoalAsync:
  → returns final Data from goal
```

### The Merge operation

When a step has multiple actions, their results are merged:

```csharp
Data.Merge(Data other) {
    // Both Values treated as List<Data>
    // For each item in other: find by Name in my list
    //   - Found: replace
    //   - Not found: append
}
```

This allows multiple actions in a single step to contribute return variables without overwriting each other.

### Return variable binding

```csharp
// In Action.RunAsync:
if (result.Value != null && this.Return != null) {
    foreach (var returnVar in this.Return)
        context.MemoryStack.Set(returnVar.Name, result.Value, result.Type);
}
```

**All return variables get the same value** — the single result value from the action. The Return list defines which variable names to bind it to.

---

## 8. Data in the Event System

### Before-events and the Handled flag

Before-events can short-circuit execution:

```csharp
// In Goal.RunAsync:
beforeResult = await lifecycle.Before.Run(context, EventType.BeforeGoal);
if (!beforeResult) return beforeResult;       // error → propagate
if (beforeResult.Handled) return beforeResult; // handled → skip goal

// In Action.RunAsync:
if (beforeResult.Handled) {
    result = beforeResult;  // use event's Data as the action result
} else {
    // run actual action handler
}
```

### EventOverride (skipAction mechanism)

`event.skipAction` sets a Data on the context:

```csharp
// In skipAction.Run():
Context.EventOverride = Data.Ok(Value);

// In EventBinding.Run():
if (Type == BeforeAction || Type == AfterAction) {
    var @override = context.EventOverride;
    if (@override != null) {
        context.EventOverride = null;    // consume it
        @override.Handled = true;        // mark as handled
        return @override;                // this Data becomes the action result
    }
}
```

The override Data flows back through the Action.RunAsync pipeline, hitting the Return variable binding. So `skipAction` can inject values into the MemoryStack.

---

## 9. Data in Step Caching

### Cache write (on miss)

```csharp
// StepCache.CollectReturnVariables:
foreach action in step.Actions:
    foreach returnVar in action.Return:
        data = memoryStack.Get(returnVar.Name)
        entry.Variables[name] = new CachedVariable {
            Value = data.Value,        // raw object reference (in-memory)
            TypeName = data.Type?.Value // PLang type name string
        }
```

Only the Value and TypeName are cached — not the full Data wrapper.

### Cache read (on hit)

```csharp
// StepCache.RestoreVariables:
foreach (name, cached) in entry.Variables:
    type = cached.TypeName != null ? new Type(cached.TypeName) : null
    memoryStack.Set(name, cached.Value, type)
```

This creates or updates Data instances in MemoryStack with the cached values. The `Set` call goes through normal Data construction (unwrapping, type inference).

### Cache key building

```csharp
// Custom key with variable resolution:
"user:%userId%:profile" → "user:42:profile"

// Default key:
"step:{goalPath}:{stepIndex}"
```

---

## 10. Data in Error Handling

### Error Data creation

```csharp
Data.FromError(IError error) => new Data("") { Error = error }
```

Error Data has:
- `Name = ""` (anonymous)
- `Value = null` (no value)
- `Error = IError` (the error)
- `Success = false`
- Implicit bool conversion returns `false`

### Error propagation

```csharp
// The `if (!result)` pattern works because of implicit bool:
var result = await step.RunAsync(engine, context, ct);
if (!result) {           // Data.Success == false → bool = false → !false = true
    if (!step.OnError?.IgnoreError ?? false)
        return result;   // propagate the error Data up
}
```

### Error variables in error goal

When a step's OnError calls an error goal:

```csharp
context.MemoryStack.Set("__error__", failedResult.Error?.Message);
context.MemoryStack.Set("__errorKey__", failedResult.Error?.Key);
context.MemoryStack.Set("__errorStatusCode__", failedResult.Error?.StatusCode);
```

These are plain string/int Data entries in MemoryStack, accessible as `%__error__%` in the error goal. Cleaned up in `finally` block.

---

## 11. Data in Serialization

### JSON serialization (for .pr files and channels)

Data has explicit JSON attributes:

```csharp
[JsonPropertyName("name")]   public string Name { get; }
[JsonPropertyName("value")]  public virtual object? Value { get; set; }
[JsonPropertyName("type")]   [JsonConverter(typeof(TypeJsonConverter))] public Type? Type { get; set; }
```

Everything else is `[JsonIgnore]`: Path, Parent, Properties, Error, Success, Handled, etc.

So a Data serializes to:
```json
{ "name": "userId", "value": 42, "type": "long" }
```

### Deserialization

`[JsonConstructor]` and `[Newtonsoft.Json.JsonConstructor]` on the main constructor ensure both serializers can create Data instances. The constructor runs `UnwrapJsonElement` on the value, so deserialized JsonElements are immediately normalized.

### View-based filtering

Data properties tagged with `[Store]`, `[LlmBuilder]`, `[Debug]`, `[Default]` are filtered per serialization view. Data itself doesn't have these attributes (they're on Goal, Step, Action), but when Data is a value inside those types, the containing type controls visibility.

---

## 12. Data in Channels

### Writing Data through channels

```csharp
// output.write action:
await Actor.Channels.WriteAsync(channel, Content);  // Content is object? from Data.Value

// EngineChannels.WriteAsync:
await _engine.Serializers.SerializeAsync(new SerializeOptions {
    Stream = channel.Stream,
    Data = data,                    // the raw object
    ContentType = contentType       // determines serializer
});
```

The channel system serializes the **unwrapped value**, not the Data wrapper. So if a Data has `Value = Dictionary<string, object?>`, that dictionary gets JSON-serialized to the stream.

### Reading Data from files

```csharp
// EngineChannels.ReadAsync<T>:
var content = await fs.File.ReadAllTextAsync(filePath);
var ext = Path.GetExtension(filePath);
return Serializers.Deserialize<T>(new DeserializeOptions { Value = content, Extension = ext });
```

For goal loading (`ReadAsync<Goal>`), the JSON deserializer creates Goal objects with nested Step/Action/Data structures. Each Data parameter in an action goes through the constructor → unwrap pipeline.

---

## 13. Data in Testing & Mocking

### Assertion results

Assert actions return Data with AssertionError:
```csharp
Data.FromError(new AssertionError("Expected 5 but got 3", context))
```

The test runner checks `__stepResult` in MemoryStack for assertion failures.

### Mock return values

```csharp
// mock.intercept with return value:
if (returnValue != null) {
    ctx.EventOverride = Data.Ok(returnValue);  // Data wrapping the mock value
    return Data.Ok(returnValue);
}
```

The mock value flows through `EventOverride` → `EventBinding.Run` → `Handled = true` → `Action.RunAsync` skips real handler → Return variables bound from the mock Data.

### Mock parameter capture

```csharp
// Captured as raw objects, not Data wrappers:
var capturedParams = new Dictionary<string, object?>();
foreach (var param in action.Parameters) {
    var value = ResolveParamValue(param, memoryStack);  // resolves %var% in Data.Value
    capturedParams[param.Name] = value;
}
```

---

## 14. Data Lifecycle Summary

### Creation points

| Where | How | Purpose |
|-------|-----|---------|
| .pr file deserialization | `new Data(name, value)` via JSON constructor | Action parameters and return definitions |
| MemoryStack.Set | `new Data(name, value, type)` | Variable storage |
| MemoryStack constructor | `new DynamicData(name, factory)` | System variables (Now, GUID) |
| PLangContext constructor | `new Data("!engine", engine)` | Context variables |
| Action.RunAsync | `Data.Ok(typedResult)` | Action return values |
| Step/Goal.RunAsync | `Data.Ok()` or `Data.FromError(error)` | Execution results |
| Data.GetChild | `new Data(segment, childValue, parent: this)` | Navigation (ephemeral) |
| StepCache.RestoreVariables | `memoryStack.Set(name, cached.Value, type)` | Cache restoration |
| EventBinding.Run | `Data.Ok()` or handler result | Event results |
| event.skipAction | `Data.Ok(Value)` on `context.EventOverride` | Action override |

### Mutation points

| Where | What changes |
|-------|-------------|
| MemoryStack.Set (existing) | `existing.Value = value` (triggers unwrap + type re-derive + Updated timestamp) |
| Action.RunAsync (return binding) | `context.MemoryStack.Set(returnVar.Name, result.Value, result.Type)` |
| EventBinding.Run | `@override.Handled = true` |
| Data.Merge | Creates new Data with merged List<Data> value |

### Data never holds

- JsonElement (unwrapped on entry)
- Newtonsoft JToken (unwrapped on entry via reflection)
- Raw CLR reference types as Type — Type is always the PLang string descriptor
- Circular parent references (parent chain is strictly one-directional upward)

### Thread safety

- Data instances are **not individually thread-safe** — Value setter is not atomic
- Thread safety comes from **MemoryStack** (ConcurrentDictionary) and the execution model (one step at a time per context)
- DynamicData is safe if the factory function is safe (DateTime.Now, Guid.NewGuid are safe)
- Navigation (GetChild) creates new instances, no shared state concerns
