# Data — Universal Value Container and Result Type

`App.Memory.Data` is the universal type in App. It serves as both the **variable container** (stored in Variables) and the **result type** (returned from all operations). It replaces the old `GoalResult`, `ObjectValue`, and `Return` types.

## Data Class

### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Variable/parameter name (auto-cleaned of `%` markers) |
| `Value` | `object?` | The stored value (auto-unwraps `JsonElement`) |
| `Type` | `Type?` | PLang type descriptor |
| `Path` | `string` | Full path (parent-qualified, e.g. `user.address.city`) |
| `Parent` | `Data?` | Parent data for nested structures |
| `IsInitialized` | `bool` | Whether a value has been set |
| `IsEmpty` | `bool` | Not initialized, null, or empty string |
| `Created` | `DateTime` | When the data was created (UTC) |
| `Updated` | `DateTime` | When the value was last set (UTC) |
| `Properties` | `Properties` | Named property collection |
| `Error` | `IError?` | Error information (if failed result) |
| `Warnings` | `List<Info>?` | Warning messages |
| `Success` | `bool` | `true` if `Error == null` |

### Implicit bool Conversion

```csharp
// Data can be used directly in if statements
public static implicit operator bool(Data d) => d.Success;

var result = await app.RunGoalAsync("Start");
if (result)  // checks Success
{
    // success path
}
```

### Constructor

```csharp
public Data(string name, object? value = null, Type? type = null, Data? parent = null)
```

- `name` is auto-cleaned: trimmed, `%` markers stripped
- `value` is auto-unwrapped if it's a `JsonElement`
- `type` is auto-derived from value if not provided (via `TypeMapping`)
- `path` is computed from parent chain

### Value Access

```csharp
T? GetValue<T>()                  // Cast or convert to T
object? GetValue(System.Type t)   // Convert to target type
Data? GetChild(string path)       // Navigate by dot notation or index
```

`GetChild` supports:
- Dot notation: `"address.city"`
- Array indexing: `"[0]"` or `"items[2].name"`
- Dictionary key access
- Object property access via reflection

### Static Factories

```csharp
// Success results
Data.Ok()                          // Success with no value
Data.Ok(value)                     // Success with value
Data.Ok(value, type)               // Success with value and type

// Failure results
Data.Fail(error)                   // Failure with IError

// Null/empty
Data.Null(name)                    // Uninitialized Data
```

### Merge

```csharp
Data Merge(Data other)
```

Treats `Value` as `List<Data>`, merges by name (replace-or-append). Used by `Actions.RunAsync` to combine results from sequential action executions.

## Type Class

`App.Memory.Type` — PLang type descriptor.

```csharp
public sealed class Type
{
    public string Value { get; }           // "string", "int", "text/markdown", etc.
    public System.Type? ClrType { get; }   // Derived via TypeMapping

    // Static factories
    public static Type String => new("string");
    public static Type Int => new("int");
    public static Type Long => new("long");
    public static Type Double => new("double");
    public static Type Bool => new("bool");
    public static Type DateTime => new("datetime");
    public static Type Object => new("object");

    public static Type FromMime(string mimeType)  // e.g., "text/markdown"
    public static Type FromName(string typeName)   // e.g., "string"
}
```

`Type.Value` is a string that can be a PLang type name (`"string"`, `"int"`) or a MIME type (`"text/markdown"`, `"image/jpeg"`). `ClrType` is derived on the fly via `TypeMapping.GetType()`.

## Data\<T\> — Generic Variant

Strongly-typed `Data` that inherits from `Data`:

```csharp
public class Data<T> : Data
{
    public new T? Value { get; set; }

    public static Data<T> Ok(T value, Type? type = null)
    public new static Data<T> Fail(IError error)
}
```

## DynamicData — Computed Values

`Data` variant that computes its value lazily via a factory function:

```csharp
public class DynamicData : Data
{
    public new object? Value => _valueFactory();  // computed on each access

    public DynamicData(string name, Func<object?> valueFactory, Type? type = null)
}
```

Used for system variables like `Now`, `NowUtc`, `GUID` in `Variables`.

## Code Examples

### Creating Variables

```csharp
var name = new Data("name", "John");
var age = new Data("age", 30, Type.Int);
var scores = new Data("scores", new List<int> { 85, 92, 78 });
```

### Checking Results

```csharp
var result = await app.RunGoalAsync("CreateUser");

if (result.Success)
{
    var user = result.GetValue<User>();
    Console.WriteLine($"Created: {user?.Name}");
}
else
{
    Console.WriteLine($"Error: {result.Error?.Message}");
}
```

### Navigating Nested Data

```csharp
var data = new Data("user", new { Name = "John", Address = new { City = "NYC" } });
var city = data.GetChild("Address.City");  // Data with Value = "NYC"
```

### JsonElement Auto-Unwrap

When deserializing from JSON, `JsonElement` values are automatically unwrapped to native .NET types:
- `JsonValueKind.String` → `string`
- `JsonValueKind.Number` → `long` or `double`
- `JsonValueKind.True/False` → `bool`
- `JsonValueKind.Null/Undefined` → `null`

## Error Philosophy

The Runtime prefers `Data.Fail` over exceptions for expected failures:

| Scenario | Approach |
|----------|----------|
| Goal not found | `Data.Fail(GoalError.NotFound(name))` |
| Action handler error | `Data.Fail(ActionError.NotFound(...))` |
| Validation error | `Data.Fail(new Error("message"))` |
| Step execution error | `Data.Fail(StepError.FromException(ex, context))` |
| Stack overflow | `CallStackOverflowException` thrown |

Exceptions are for truly exceptional cases (programming errors, resource exhaustion). Expected operational errors use `Data.Fail`.

## Relationships

- Returned by [App](app.md) methods
- Returned by [Action handlers](modules.md) via `CodeGeneratedExecuteAsync`
- Returned by [IO](io-channels.md) read/write operations
- Stored in [Variables](memory-stack.md) as variable entries
- Contains [IError](exceptions.md) for failures
- Uses [Type](#type-class) for type metadata
- Uses [TypeMapping](modules.md) for type derivation and conversion
