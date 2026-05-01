# Variables & Variables

Variable storage using `Data` entries with type metadata, dot-notation path resolution, and system variables.

## Variables

`App.Memory.Variables` — `ConcurrentDictionary<string, Data>` under the hood.

### API Surface

```csharp
public class @this   // App.Variables.@this
{
    // Core operations
    Data Set(Data value)                                  // Stores Data under value.Name
    Data Set(string name, object? value, Type? type = null)  // Wraps non-Data values
    Data Get(string name)                                 // Get Data by name (dot-notation path support)
    T? Get<T>(string name)                                // Get typed value
    object? GetValue(string name)                         // Get raw value
    bool Contains(string name)
    bool Remove(string name)                              // Fires OnDelete on the removed Data
    void Clear()                                          // Drops non-system variables

    // Enumeration
    IEnumerable<string> GetNames()
    IEnumerable<KeyValuePair<string, Data>> GetAll()

    // Cloning + snapshots
    @this Clone()
    HashSet<string> Save()
    void Restore(HashSet<string> snapshot)

    // Conversion / diagnostics
    Dictionary<string, object?> ToDictionary(bool includeSystem = false)
    Dictionary<string, object?> Snapshot()                // for assertion/error diagnostics
    string Resolve(string input, bool skipInfrastructure = false)
}
```

### Behavior & Rules

- Variable names are **case-insensitive**
- Thread-safe via `ConcurrentDictionary`
- `Set(Data)` aliases the Data under its `Name`. The dictionary key is the source of truth for lookups; `Data.Name` stays advisory.
- `Set(name, value, type)` for a non-Data value updates the existing Data in-place (so readers holding the prev reference see the new value); when no entry exists yet, a fresh Data is constructed and stored.
- On replacement of an existing Data binding: **event subscribers (`OnCreate`/`OnChange`/`OnDelete`) follow the name** — the new Data aliases the prev binding's event-list refs so `--debug={"variables":[...]}` watches see every assignment, not just the first. **`Properties` stay with the Data instance** — they're result metadata, not binding metadata.
- `variable.set` is the sole binding-mint site for user-visible variables; it owns type inference (`MintTyped` picks the concrete `Data<T>` for the runtime type) and snapshot-clones mutable refs (List, Dict) so source/target don't alias.
- `Get(name)` supports dot-notation paths (e.g., `"user.address.city"`) and bracket indices with variables (`addresses[idx].street`) — splits on first separator and delegates to `Data.GetChild()`
- `Remove(name)` fires `OnDelete` on the removed Data so subscribers (e.g. `event=ondelete` debug watches) see the deletion.
- `Clone()` deep-clones each Data so mutations in the clone do not affect the original.
- `ToDictionary(includeSystem)` returns `Dictionary<string, object?>` of variable values (excludes `!`-prefixed by default).
- `Snapshot()` is for failure diagnostics — excludes infrastructure (`!`-prefixed), dynamic system vars (`Now`/`NowUtc`/`GUID`), and `SettingsVariable`.

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
var memory = new Variables.@this();

// Set variables (raw values are wrapped into Data on the way in)
memory.Set("name", "John");
memory.Set("age", 30, Data.Type.Int);
memory.Set("scores", new List<int> { 85, 92, 78 });

// Set a Data instance directly — preserves identity (Properties + event subscribers)
memory.Set(new Data.@this<string>("greeting", "hi"));

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

// Remove (fires OnDelete on the removed Data)
memory.Remove("age");

// System variables (always available, computed on access)
var now = memory.Get("Now");     // DynamicData, Value = DateTimeOffset.Now
var guid = memory.Get("GUID");   // DynamicData, Value = new Guid each access

// Clone (deep)
var copy = memory.Clone();

// IVariablesAccessor — AsyncLocal-based accessor for implicit Variables threading
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

The Runtime resolves `%name%`, `%age%`, and `%user.id%` by calling `Variables.Get()` with dot-notation path navigation. The source generator emits a `partial class` extension on the action record itself; properties resolve `%var%` lazily on first access via `Action.GetParameter(name).As<T>(Context)` (typed slots) or `.AsCanonical(Context)` (plain `Data` slots that operate on the live variable directly).

## Relationships

- Stored in [PLangContext](contexts.md)
- Variables are [Data](goal-result.md) objects
- Used by [App](app.md) to store action return values
- Used by action handlers (e.g., `variable.set`) for variable operations
- Cloned for [CallStack](call-stack.md) change tracking when debugging
- Type metadata uses [Type](goal-result.md) and [TypeMapping](modules.md)
