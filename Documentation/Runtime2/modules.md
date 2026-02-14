# Action Handlers

Action handlers provide the executable functionality for PLang steps. Each handler class exposes typed parameter records and a source-generated `CodeGeneratedExecuteAsync` entry point.

## IClass Interface

`PLang.Runtime2.modules.IClass` — base interface for all action handlers.

```csharp
public interface IClass
{
    Engine Engine { get; set; }
    PLangContext Context { get; set; }
    System.Type ParameterType { get; }

    Task Initialize(PLangContext context);
    Task<Data> ExecuteAsync(string method, List<Data> parameters);
}
```

## ICodeGenerated Interface

`PLang.Runtime2.modules.ICodeGenerated` — source-generated dispatch interface. **Required** on all handlers — Engine has no fallback path.

```csharp
public interface ICodeGenerated
{
    Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context);
}
```

The PLang source generator (`PLang.Generators/LazyParamsGenerator.cs`) scans handler classes and generates a partial implementation that:
1. Creates a `*__Generated` record from the parameter list
2. Resolves `%var%` references lazily at property access time
3. Dispatches to the correct handler method based on the action's `Method` name

## BaseClass

`PLang.Runtime2.modules.BaseClass` — abstract base class with common handler functionality.

```csharp
public abstract class BaseClass : IClass
{
    // From IClass
    public Engine Engine { get; set; }
    public PLangContext Context { get; set; }
    public abstract System.Type ParameterType { get; }

    // Convenience properties
    protected MemoryStack MemoryStack => Context.MemoryStack;

    // Result helpers
    protected Data Success(object? value = null)
    protected Data Error(string message, string key = "Error", int statusCode = 400)
    protected Task<Data> SuccessTask(object? value = null)
    protected Task<Data> ErrorTask(string message, string key = "Error", int statusCode = 400)
}
```

### BaseClass\<TParams\>

Generic variant that provides typed parameter access:

```csharp
public abstract class BaseClass<TParams> : BaseClass
{
    public override System.Type ParameterType => typeof(TParams);
}
```

## Library

`PLang.Runtime2.modules.Library` — a single library representing one assembly's action handlers.

```csharp
public sealed class Library
{
    string Name { get; }
    Assembly? Assembly { get; }

    // Discovery
    void Discover(string? baseNamespace = null)   // Finds [Action]-attributed types

    // Registration
    void Register(string module, string actionName, IClass handler)     // Shared instance
    void RegisterCodeGenerated(string module, string actionName, Type type)  // Per-call

    // Lookup
    IClass? Get(string module, string actionName)
    ICodeGenerated? GetCodeGenerated(string module, string actionName)
    Type? GetActionType(string module, string actionName)
    bool Contains(string module, string actionName)
    bool Contains(string module)

    // Enumeration
    IEnumerable<string> Modules { get; }
    IEnumerable<string> GetActions(string module)
    int Count { get; }
}
```

## Libraries

`PLang.Runtime2.modules.Libraries` — smart collection of libraries. Owns walk-the-list handler resolution. Built-in library is always `[0]`. External DLLs are added as additional libraries.

```csharp
public sealed class Libraries
{
    Library BuiltIn { get; }                    // Always [0]
    IReadOnlyList<Library> Value { get; }       // All libraries

    // Resolution (walks all libraries, first match wins)
    (ICodeGenerated? Handler, IError? Error) GetCodeGenerated(
        string module, string actionName, PLangContext context)

    // Library management
    void Add(Library library)

    // Convenience delegates to BuiltIn
    void Register(string module, string actionName, IClass handler)
    void RegisterCodeGenerated(string module, string actionName, Type type)

    // Aggregate queries across all libraries
    bool Contains(string module, string actionName)
    bool Contains(string module)
    IEnumerable<string> Modules { get; }
    IEnumerable<string> GetActions(string module)
    Type? GetActionType(string module, string actionName)
    int Count { get; }
}
```

### Behavior & Rules

- Built-in library auto-discovers PLang's own `[Action]` types on construction
- `GetCodeGenerated` walks all libraries in order — first match wins
- Explicit instances (`Register`) take priority over type-registered handlers (`RegisterCodeGenerated`)
- Type-registered handlers create a new instance per call (thread-safe)
- Lookup is case-insensitive
- External libraries can be added at runtime via `Libraries.Add(library)` or the `library.load` handler

## Creating an Action Handler

### Handler Pattern

Each action handler is a single `partial class` with:
1. An `[Action("name")]` attribute — identifies the action
2. `IContext` implementation — receives `PLangContext`
3. `partial` properties — source generator auto-implements with lazy `%var%` resolution
4. A `Run()` method — contains the execution logic

### Example: variable.set

```csharp
// PLang/Runtime2/modules/variable/set.cs

namespace PLang.Runtime2.modules.variable;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    [VariableName]
    public partial string Name { get; init; }
    public partial object? Value { get; init; }
    public partial string? Type { get; init; }

    public Task<Data> Run()
    {
        Context.MemoryStack.Set(Name, Value,
            Type != null ? Memory.Type.FromName(Type) : null);
        return Task.FromResult(Data.Ok(
            new types.variable { name = Name, value = Value, type = Type }));
    }
}
```

The source generator creates a partial implementation that:
1. Auto-implements the `partial` properties with lazy `%var%` resolution from MemoryStack
2. Implements `ICodeGenerated.CodeGeneratedExecuteAsync` to wire Context and call `Run()`
3. Uses `[VariableName]` to strip `%` markers instead of resolving the variable value

### Handler Naming Convention

| Component | Convention | Example |
|-----------|-----------|---------|
| Parameter record | Lowercase action name | `set`, `save`, `read` |
| Handler class | PascalCase + "Handler" | `SetHandler`, `SaveHandler`, `ReadHandler` |
| Namespace | `PLang.Runtime2.modules.{module}` | `PLang.Runtime2.modules.variable` |
| File | `{action}.cs` | `set.cs`, `save.cs` |

### Action Reference in .pr JSON

```json
{
  "action": "variable",
  "method": "set",
  "parameters": [
    { "name": "name", "value": "greeting" },
    { "name": "value", "value": "Hello World" }
  ]
}
```

The `"action"` field maps to the handler namespace/class, and `"method"` maps to the handler method.

## TypeMapping

`PLang.Runtime2.Utility.TypeMapping` — maps between PLang type names, MIME types, and .NET types.

```csharp
public static class TypeMapping
{
    System.Type? GetType(string typeName)         // PLang name → CLR type
    string GetTypeName(System.Type type)          // CLR type → PLang name
    bool IsPrimitive(System.Type type)
    object? ConvertTo(object? value, System.Type targetType)
    string? GetMimeType(string typeName)          // PLang name → MIME type
}
```

### Type Mappings

| PLang Type | .NET Type |
|------------|-----------|
| `string`, `text` | `string` |
| `int`, `integer` | `int` |
| `long` | `long` |
| `float` | `float` |
| `double`, `number` | `double` |
| `decimal` | `decimal` |
| `bool`, `boolean` | `bool` |
| `datetime`, `date` | `DateTime` |
| `time`, `timespan` | `TimeSpan` |
| `guid` | `Guid` |
| `list` | `List<object>` |
| `list<T>` | `List<T>` |
| `dict`, `dictionary`, `map` | `Dictionary<string, object>` |
| `dict<K,V>` | `Dictionary<K,V>` |
| MIME types (e.g., `text/markdown`) | Mapped via TypeMapping |

## Built-in Action Handlers

| Namespace | Actions | Purpose |
|-----------|---------|---------|
| `variable` | `set`, `get`, `remove`, `exists`, `clear` | Variable operations |
| `file` | `save`, `read`, `copy`, `move`, `delete`, `exists`, `list` | File operations |
| `output` | `write` | Console/channel output |
| `condition` | Condition evaluation | If/else logic |
| `library` | `load` | Load external DLL libraries |

## Relationships

- Registered in [Engine](engine.md) via `Libraries` property (`Libraries`)
- Receive `PLangContext` via `IContext` and `CodeGeneratedExecuteAsync`
- Access [MemoryStack](memory-stack.md) for variable operations
- Return [Data](goal-result.md) from execution
- Referenced by [Action](goals-steps.md) via `Module` and `ActionName`
- Source generator in `PLang.Generators/LazyParamsGenerator.cs`
