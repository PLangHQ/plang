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

`PLang.Runtime2.actions.ICodeGenerated` — source-generated dispatch interface. Handlers don't implement this directly — the source generator adds it automatically. Engine requires it at runtime (no fallback path).

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
2. Adds `ICodeGenerated.CodeGeneratedExecuteAsync` to wire Context and call `Run()` (handlers never implement this directly)
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
| `condition` | `if`, `compare` | Conditional branching and comparison |
| `identity` | `create`, `get`, `getAll`, `archive`, `unarchive`, `rename`, `setDefault`, `export` | Ed25519 identity management |
| `library` | `load` | Load external DLL libraries |

### condition module — Details

The condition module uses structured `Left/Operator/Right` parameters (not expression strings). The LLM builder maps natural language conditions to these typed parameters.

**`condition.if`** — Evaluates and branches. Two modes:
- **Goal mode**: `GoalIfTrue`/`GoalIfFalse` set — calls the appropriate goal.
- **Sub-step mode**: No goals — returns a bool. `Steps.RunAsync` uses the `__condition__` MemoryStack signal to skip/execute indented children.

When `Operator` is null, performs a truthy check on `Left`. When set, evaluates `Left op Right` via `IEvaluator`.

**`condition.compare`** — Pure boolean evaluation. Returns a bool wrapped in `Data`. Used as an intermediate in compound conditions (AND/OR) where multiple `compare` results feed into a final `if`. Does NOT set `__condition__` — only `if` controls sub-step execution.

**Pluggable evaluator**: Both actions use `IEvaluator` (default: `DefaultEvaluator`). Supports operators: `==`, `!=`, `>`, `<`, `>=`, `<=`, `contains`, `startswith`, `endswith`, `in`, `isempty`, `not`, `and`, `or`. Type normalization widens numeric operands automatically.

### identity module — Details

The identity module manages Ed25519 key pairs stored in the System actor's DataSource (`identity` table). Each identity has a name, public/private key pair, default flag, and archive flag.

**Core type — `IdentityVariable`**: OBP-compliant entity that owns its persistence (`LoadAsync`, `SaveAsync`, `RemoveAsync` navigate to `engine.System.DataSource`). The `PrivateKey` property is marked `[Sensitive]` — excluded from output serialization but persisted in storage and accessible via `%MyIdentity.PrivateKey%` in PLang code. `ToString()` returns the public key, so `%MyIdentity%` in string context gives the public key.

**Lazy resolution — `IdentityData`**: A `Data` subclass on `Actor.Identity` that lazily resolves the default identity on first access. Auto-creates a "default" identity if none exist. Uses sync-over-async (safe in PLang's sequential execution model with no SynchronizationContext). Handlers call `Update()` after changing the default.

**`%MyIdentity%`**: Registered on every actor's MemoryStack as `DynamicData` pointing to `engine.System.Identity.Value`. Re-evaluates on each access, so changes via `setDefault` or `rename` are reflected immediately.

**Actions:**

| Action | Parameters | Behavior |
|--------|-----------|----------|
| `create` | `Name` (default: "default"), `SetAsDefault` (default: false) | Creates identity with Ed25519 key pair. Validates name uniqueness (case-insensitive, includes archived). |
| `get` | `Name` (optional) | By name: returns identity or 404. No name: returns default, auto-creates if needed. |
| `getAll` | — | Lists all non-archived identities. |
| `archive` | `Name` | Soft-deletes. Cannot archive the default — set a different default first. Idempotent. |
| `unarchive` | `Name` | Restores archived identity. Idempotent. |
| `rename` | `Name`, `NewName` | Atomic rename: save-new-first, then remove-old (no data loss on failure). Updates `%MyIdentity%` if default. |
| `setDefault` | `Name` | Switches default. Cannot set archived identity. Clears all existing defaults. Idempotent. |
| `export` | `Name` (optional) | Returns raw private key string. By name or default (auto-creates if needed). |

All mutating actions are `Cacheable = false`. All return `Data` — errors use `ActionError` (validation) or `ServiceError` (save failures).

## Relationships

- Registered in [Engine](engine.md) via `Libraries` property (`Libraries`)
- Receive `PLangContext` via `IContext` and `CodeGeneratedExecuteAsync`
- Access [MemoryStack](memory-stack.md) for variable operations
- Return [Data](goal-result.md) from execution
- Referenced by [Action](goals-steps.md) via `Module` and `ActionName`
- Source generator in `PLang.Generators/LazyParamsGenerator.cs`
