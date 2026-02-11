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

## ActionRegistry

`PLang.Runtime2.modules.ActionRegistry` — two-level `ConcurrentDictionary` for handler lookup.

```csharp
public sealed class ActionRegistry
{
    // Registration
    void Register(string namespaceName, string className, IClass handler)
    void DiscoverAndRegister(Assembly assembly)   // Reflection-based discovery

    // Lookup
    IClass? Get(string namespaceName, string className)
    ICodeGenerated? GetCodeGenerated(string className, string methodName)

    // Enumeration
    IEnumerable<string> Namespaces { get; }
    IEnumerable<string> ClassNames(string namespaceName)
}
```

### Behavior & Rules

- Two-level structure: `namespace → className → IClass`
- `DiscoverAndRegister` scans an assembly for all types implementing `IClass`
- `GetCodeGenerated` finds the handler and casts to `ICodeGenerated`
- Lookup is case-insensitive
- Engine calls `RegisterBuiltInModules()` in constructor, which calls `DiscoverAndRegister(Assembly.GetExecutingAssembly())`

## Creating an Action Handler

### Handler Pattern

Each action handler consists of:
1. A **parameter record** (lowercase name matching the action) — defines the typed parameters
2. A **handler class** (PascalCase + "Handler") — contains the execution logic as a `partial class`
3. A **source-generated partial** — implements `ICodeGenerated` with lazy parameter resolution

### Example: variable.set

```csharp
// PLang/Runtime2/actions/variable/set.cs

namespace PLang.Runtime2.modules.variable;

// Parameter record — defines the action's typed parameters
public record set(string name, object? value, string? type);

// Handler class — contains execution logic
public partial class SetHandler : BaseClass<set>
{
    public Data Execute(set parameters)
    {
        var type = parameters.type != null
            ? new Memory.Type(parameters.type)
            : null;

        MemoryStack.Set(parameters.name, parameters.value, type);
        return Success();
    }
}
```

The source generator creates a `SetHandler` partial that implements `ICodeGenerated`:

```csharp
// Auto-generated (conceptual)
public partial class SetHandler : ICodeGenerated
{
    // Generated record that resolves %var% at property access
    public sealed record set__Generated(/* ... */) : set(/* ... */);

    public Task<Data> CodeGeneratedExecuteAsync(List<Data> parameters, Engine engine, PLangContext context)
    {
        Engine = engine;
        Context = context;

        // Create lazy params that resolve %var% from MemoryStack
        var p = new set__Generated(/* map parameters by name */);

        return Task.FromResult(Execute(p));
    }
}
```

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

## Relationships

- Registered in [Engine](engine.md) via `Actions` property (`ActionRegistry`)
- Receive `PLangContext` via `CodeGeneratedExecuteAsync`
- Access [MemoryStack](memory-stack.md) for variable operations
- Return [Data](goal-result.md) from execution
- Referenced by [Action](goals-steps.md) via `Class` and `Method`
- Source generator in `PLang.Generators/LazyParamsGenerator.cs`
