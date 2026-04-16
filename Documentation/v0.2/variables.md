# Variables & Variables

Variable storage using `Data` entries with type metadata, dot-notation path resolution, and system variables.

## Variables

`App.Memory.Variables` — `ConcurrentDictionary<string, Data>` under the hood.

### API Surface

```csharp
public sealed class Variables
{
    // Core operations
    void Put(Data data)                           // Add/replace a Data entry
    void Set(string name, object? value, Type? type = null)  // Create and add
    Data? Get(string name)                        // Get Data by name (dot-notation path support)
    T? Get<T>(string name)                        // Get typed value
    object? GetValue(string name)                 // Get raw value
    bool Contains(string name)
    bool Remove(string name)
    void Clear()

    // Enumeration
    IEnumerable<string> GetNames()
    IEnumerable<Data> GetAll()

    // Cloning
    Variables Clone()

    // Conversion
    Dictionary<string, object?> ToDictionary()
}
```

### Behavior & Rules

- Variable names are **case-insensitive**
- Thread-safe via `ConcurrentDictionary`
- `Put(data)` adds a `Data` object directly
- `Set(name, value, type)` creates a new `Data` and puts it
- `Get(name)` supports dot-notation paths (e.g., `"user.address.city"`) — splits on first `.` and delegates to `Data.GetChild()`
- `Clone()` creates a shallow copy of all variables
- `ToDictionary()` returns `Dictionary<string, object?>` of all variable values

### System Variables

Registered as `DynamicData` (computed on each access):

| Variable | Description | Returns |
|----------|-------------|---------|
| `Now` | Current local time | `DateTime.Now` |
| `NowUtc` | Current UTC time | `DateTime.UtcNow` |
| `GUID` | New unique identifier | `Guid.NewGuid().ToString()` |

### Action Result Variable

Every action stores its result as `%__data__%` on the context's Variables after execution. This is how data flows between actions in a step:

```plang
- read file 'config.json'         / result → %__data__%
- set %config% = %__data__%       / variable.set reads %__data__% from previous action
```

The builder produces a `variable.set` action whenever a step needs to capture a result into a named variable. The `%__data__%` variable is overwritten by each action, so it always holds the most recent result.

### Code Examples

```csharp
var memory = new Variables();

// Set variables
memory.Set("name", "John");
memory.Set("age", 30, Type.Int);
memory.Set("scores", new List<int> { 85, 92, 78 });

// Get variables
var nameData = memory.Get("name");         // Data { Name = "name", Value = "John" }
var name = memory.Get<string>("name");     // "John"
var age = memory.GetValue("age");          // 30 (as object)

// Dot-notation path resolution
memory.Set("user", new { Name = "John", Address = new { City = "NYC" } });
var city = memory.Get("user.Address.City");  // Data { Value = "NYC" }

// Check existence
if (memory.Contains("name"))
{
    // variable exists
}

// Remove
memory.Remove("age");

// System variables (always available)
var now = memory.Get("Now");     // DynamicData, Value = DateTime.Now
var guid = memory.Get("GUID");   // DynamicData, Value = new Guid each access

// Clone
var copy = memory.Clone();

// IVariablesAccessor/VariablesAccessor
// AsyncLocal-based accessor for implicit Variables threading
```

## Data (as Variable Container)

Each variable in Variables is stored as a `Data` object. See [Data](goal-result.md) for full documentation.

Key properties for variable use:
- `Name` — variable identifier (auto-cleaned of `%` markers)
- `Value` — the stored value
- `Type` — PLang type descriptor
- `Path` — parent-qualified path (e.g., `user.address.city`)
- `GetChild(path)` — navigate to nested properties
- `GetValue<T>()` — get typed value

## Properties

`App.Memory.Properties : IList<Data>` — named collection of `Data` items.

```csharp
public class Properties : IList<Data>
{
    // Named access
    Data? this[string name] { get; set; }

    // Typed access
    T? Get<T>(string name)
    void Set(string name, object? value)
    bool Remove(string name)

    // Conversion
    Dictionary<string, object?> ToDictionary()
}
```

Used by `Data.Properties` for attaching metadata to variables.

## Variable Syntax in PLang

In PLang, `%variable%` syntax maps to Variables lookups at runtime:

```plang
CreateUser
- set %name% to "John"
- set %age% to 30
- insert into users, name=%name%, age=%age%, write to %user%
- log "Created user %user.id%"
```

The Runtime resolves `%name%`, `%age%`, and `%user.id%` by calling `Variables.Get()` with dot-notation path navigation. The source generator creates lazy parameter records (`*__Generated`) that resolve `%var%` references at property access time.

## Relationships

- Stored in [PLangContext](contexts.md)
- Variables are [Data](goal-result.md) objects
- Used by [App](app.md) to store action return values
- Used by action handlers (e.g., `variable.set`) for variable operations
- Cloned for [CallStack](call-stack.md) change tracking when debugging
- Type metadata uses [Type](goal-result.md) and [TypeMapping](modules.md)
