# MemoryStack & Variables

Variable storage with type metadata and system variables. Variables set in PLang steps are stored here.

## MemoryStack

### API Surface

```csharp
public sealed class MemoryStack
{
    // System variables (read-only)
    public DateTime Now { get; }
    public DateTime NowUtc { get; }
    public string NewGuid { get; }

    // Variable operations
    public void Set(string name, object? value, TypeInfo? typeInfo = null)
    public ObjectValue? Get(string name)
    public object? GetValue(string name)
    public T? GetValue<T>(string name)
    public bool TryGet(string name, out ObjectValue? value)
    public bool Contains(string name)
    public bool Remove(string name)
    public void Clear()

    // Enumeration
    public IEnumerable<string> GetNames()
    public IEnumerable<ObjectValue> GetAll()

    // Cloning
    public MemoryStack Clone()
}
```

### Behavior & Rules

- Variable names are case-insensitive
- Thread-safe via `ConcurrentDictionary`
- `Set` creates or updates a variable with optional type information
- `Get` returns `ObjectValue` wrapper, `GetValue` returns the raw value
- `Clone()` creates a shallow copy of all variables

### System Variables

| Variable | Description | Returns |
|----------|-------------|---------|
| `Now` | Current local time | `DateTime.Now` |
| `NowUtc` | Current UTC time | `DateTime.UtcNow` |
| `NewGuid` | New unique identifier | `Guid.NewGuid().ToString()` |

System variables are computed on each access.

### Code Examples

```csharp
var memory = new MemoryStack();

// Set variables
memory.Set("name", "John");
memory.Set("age", 30);
memory.Set("scores", new List<int> { 85, 92, 78 }, new TypeInfo("list<int>"));

// Get variables
var name = memory.GetValue<string>("name");     // "John"
var age = memory.GetValue<int>("age");          // 30
var obj = memory.Get("name");                   // ObjectValue

// Check existence
if (memory.Contains("name"))
{
    // variable exists
}

// Remove
memory.Remove("age");

// System variables
var now = memory.Now;
var guid = memory.NewGuid;

// Clone
var copy = memory.Clone();
```

## ObjectValue

Wraps a variable value with name and type metadata.

### API Surface

```csharp
public class ObjectValue
{
    // Properties
    public string Name { get; }
    public object? Value { get; set; }
    public TypeInfo? TypeInfo { get; set; }

    // Constructor
    public ObjectValue(string name, object? value = null, TypeInfo? typeInfo = null)

    // Value access
    public T? GetValue<T>()
    public ObjectValue? GetChild(string path)

    // Child properties
    public Properties Children { get; }
}
```

### Behavior & Rules

- `Name` — variable identifier
- `Value` — the stored value
- `TypeInfo` — optional type metadata
- `GetValue<T>()` — returns value cast to T
- `GetChild(path)` — navigates to child property by dot-separated path
- `Children` — collection of child properties

### Code Examples

```csharp
var obj = new ObjectValue("user", new { Name = "John", Age = 30 });

// Get typed value
var user = obj.GetValue<User>();

// Navigate to child
var nameChild = obj.GetChild("Name");
```

## DynamicObjectValue

Extends ObjectValue with dynamic property access for object/dictionary values.

```csharp
var dynamic = new DynamicObjectValue("data", new Dictionary<string, object>
{
    ["name"] = "John",
    ["address"] = new { City = "NYC", Zip = "10001" }
});

// Access nested properties
var city = dynamic.GetChild("address.City");
```

## TypeInfo

Type metadata for variables.

### API Surface

```csharp
public sealed class TypeInfo
{
    // Properties
    public string Name { get; }
    public Type? ClrType { get; }
    public bool IsList { get; }
    public bool IsDictionary { get; }
    public bool IsNullable { get; }

    // Constructor
    public TypeInfo(string name)

    // Static factories
    public static TypeInfo FromType(Type type)
    public static TypeInfo FromType<T>()
}
```

### Code Examples

```csharp
var stringType = new TypeInfo("string");
var listType = new TypeInfo("list<int>");
var dictType = new TypeInfo("dict<string,object>");

// From CLR type
var typeInfo = TypeInfo.FromType<List<string>>();  // "list<string>"
```

## Properties

A collection of ObjectValue items with named access.

### API Surface

```csharp
public class Properties : IList<ObjectValue>
{
    // Indexers
    public ObjectValue this[int index] { get; set; }
    public ObjectValue? this[string name] { get; set; }

    // Methods
    public T? Get<T>(string name)
    public void Set(string name, object? value, TypeInfo? typeInfo = null)
    public bool Remove(string name)
    public bool Contains(string name)
    public Dictionary<string, object?> ToDictionary()
}
```

### Code Examples

```csharp
var props = new Properties();
props.Set("name", "John");
props.Set("age", 30);

var name = props.Get<string>("name");
var dict = props.ToDictionary();
```

## Variable Syntax in PLang

In PLang, `%variable%` syntax maps to MemoryStack lookups at runtime:

```plang
CreateUser
- set %name% to "John"
- set %age% to 30
- insert into users, name=%name%, age=%age%, write to %user%
- log "Created user %user.id%"
```

The Runtime resolves `%name%`, `%age%`, and `%user.id%` by calling `MemoryStack.GetValue` with dot-notation path navigation.

## Relationships

- Stored in [PLangContext](contexts.md)
- Used by [Engine](engine.md) to store step return values
- Used by [VariableModule](modules.md) for variable operations
- Cloned for [CallStack](call-stack.md) change tracking when debugging
