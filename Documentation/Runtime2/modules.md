# Modules

Modules provide the executable functionality for PLang steps. Each module exposes methods that can be called from PLang code.

## IModule Interface

### API Surface

```csharp
public interface IModule
{
    // Identity
    string Name { get; }
    IEnumerable<string> Aliases { get; }

    // Lifecycle
    void Initialize(ModuleContext context);

    // Execution
    Task<GoalResult> ExecuteAsync(string method, object? parameters);

    // Discovery
    bool CanHandle(string method);
    IEnumerable<string> GetMethods();
}
```

### Behavior & Rules

- `Name` — primary identifier for the module
- `Aliases` — alternative names (e.g., "db" as alias for "database")
- `Initialize` — called before each `ExecuteAsync` with current execution context
- `ExecuteAsync` — single entry point, dispatches based on method name
- `CanHandle` — returns true if module supports the given method
- `GetMethods` — returns all supported method names

## ModuleContext

Provided to modules during initialization.

```csharp
public sealed class ModuleContext
{
    public Engine Engine { get; }
    public PLangContext Context { get; }
    public Goal? Goal { get; }
    public Step? Step { get; }
}
```

## BaseModule

Abstract base class with common module functionality.

### API Surface

```csharp
public abstract class BaseModule : IModule
{
    // Properties
    public abstract string Name { get; }
    public virtual IEnumerable<string> Aliases => Array.Empty<string>();

    // Context access
    protected Engine Engine { get; private set; }
    protected PLangContext Context { get; private set; }
    protected Goal? Goal { get; private set; }
    protected Step? Step { get; private set; }
    protected MemoryStack MemoryStack => Context.MemoryStack;

    // Lifecycle
    public void Initialize(ModuleContext context)

    // Execution (must be implemented)
    public abstract Task<GoalResult> ExecuteAsync(string method, object? parameters);

    // Discovery
    public virtual bool CanHandle(string method) => false;
    public virtual IEnumerable<string> GetMethods() => Array.Empty<string>();
}
```

## ModuleRegistry

Manages registered modules.

### API Surface

```csharp
public sealed class ModuleRegistry
{
    // Registration
    public void Register(IModule module)
    public bool Unregister(string name)

    // Lookup
    public IModule? Get(string name)
    public bool Contains(string name)
    public IModule? FindByMethod(string method)

    // Enumeration
    public IEnumerable<string> Names { get; }
    public IEnumerable<IModule> All { get; }
}
```

### Behavior & Rules

- Name lookup is case-insensitive
- Registers module by `Name` and all `Aliases`
- `FindByMethod` iterates modules calling `CanHandle`

## Creating a Module

### C# Implementation

```csharp
public class DbModule : BaseModule
{
    public override string Name => "db";
    public override IEnumerable<string> Aliases => new[] { "database", "sql" };

    public override async Task<GoalResult> ExecuteAsync(string method, object? parameters)
    {
        return method.ToLowerInvariant() switch
        {
            "insert" => await InsertAsync(parameters),
            "select" => await SelectAsync(parameters),
            "update" => await UpdateAsync(parameters),
            "delete" => await DeleteAsync(parameters),
            _ => GoalResult.Fail($"Unknown method: {method}", "MethodNotFound", 404)
        };
    }

    public override bool CanHandle(string method)
    {
        return method.ToLowerInvariant() is "insert" or "select" or "update" or "delete";
    }

    public override IEnumerable<string> GetMethods()
    {
        return new[] { "insert", "select", "update", "delete" };
    }

    private async Task<GoalResult> InsertAsync(object? parameters)
    {
        // Extract parameters
        var dict = parameters as Dictionary<string, object?>;
        var table = dict?["table"]?.ToString();
        var data = dict?["data"];

        // Perform insert
        var id = await _database.InsertAsync(table, data);

        return GoalResult.Ok(new { id });
    }

    // ... other methods
}
```

### Registration

```csharp
await using var engine = new Engine(appContext);
engine.Modules.Register(new DbModule());
```

### PLang Usage

```plang
CreateUser
- insert into users, name=%name%, email=%email%, write to %user%
- select * from users where id=%user.id%, return 1, write to %result%
```

The compiler maps this to:
- Module: `db`
- Method: `insert` / `select`
- Parameters: extracted from the natural language

## Built-in VariableModule

Provides variable manipulation operations.

```csharp
public class VariableModule : BaseModule
{
    public override string Name => "variable";

    public override Task<GoalResult> ExecuteAsync(string method, object? parameters)
    {
        return method.ToLowerInvariant() switch
        {
            "set" => SetAsync(parameters),
            "get" => GetAsync(parameters),
            "remove" => RemoveAsync(parameters),
            "exists" => ExistsAsync(parameters),
            "clear" => ClearAsync(),
            _ => GoalResult.FailTask($"Unknown method: {method}")
        };
    }

    private Task<GoalResult> SetAsync(object? parameters)
    {
        var dict = parameters as Dictionary<string, object?>;
        var name = dict?["name"]?.ToString();
        var value = dict?["value"];

        if (string.IsNullOrEmpty(name))
            return GoalResult.FailTask("Variable name is required");

        MemoryStack.Set(name, value);
        return GoalResult.SuccessTask();
    }

    // ... other methods
}
```

### PLang Usage

```plang
SetVariables
- set variable %name% to "John"
- set variable %count% to 0
- get variable %name%, write to %result%
- remove variable %temp%
- if variable %flag% exists then call HandleFlag
- clear all variables
```

## TypeMapping

Maps between PLang type names and .NET types.

### API Surface

```csharp
public static class TypeMapping
{
    public static Type? GetType(string typeName)
    public static string GetTypeName(Type type)
    public static bool IsPrimitive(Type type)
    public static object? ConvertTo(object? value, Type targetType)
}
```

### Type Mappings

| PLang Type | .NET Type |
|------------|-----------|
| `string`, `text` | `string` |
| `int`, `integer` | `int` |
| `long` | `long` |
| `float` | `float` |
| `double` | `double` |
| `decimal` | `decimal` |
| `bool`, `boolean` | `bool` |
| `datetime`, `date` | `DateTime` |
| `time`, `timespan` | `TimeSpan` |
| `guid` | `Guid` |
| `list` | `List<object>` |
| `list<T>` | `List<T>` |
| `dict`, `dictionary`, `map` | `Dictionary<string, object>` |
| `dict<K,V>` | `Dictionary<K,V>` |

### Code Examples

```csharp
// Get .NET type from PLang name
var type = TypeMapping.GetType("list<int>");  // typeof(List<int>)

// Get PLang name from .NET type
var name = TypeMapping.GetTypeName(typeof(Dictionary<string, int>));  // "dict<string,int>"

// Convert values
var result = TypeMapping.ConvertTo("123", typeof(int));  // 123
```

## Relationships

- Registered in [Engine](engine.md) via `Modules` property
- Receive [ModuleContext](#modulecontext) during execution
- Access [MemoryStack](memory-stack.md) for variable operations
- Return [GoalResult](goal-result.md) from `ExecuteAsync`
- Referenced by [Step](goals-steps.md) via `ModuleName`
