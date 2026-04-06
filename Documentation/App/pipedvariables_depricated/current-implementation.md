# Current Variable Resolution — How It Works in C#

This document describes exactly how PLang App resolves `%variable%` references today, so you have full context for designing the pipe system.

## Overview: Three Resolution Points

Variable references (`%var%`) are resolved in three places in the codebase:

1. **Variables.Get()** — the core variable store, handles dot notation and indexing
2. **Source-generated lazy params (`__Resolve<T>`)** — resolves `%var%` inside action parameters at property access time
3. **TString.Resolve()** — resolves `%var%` inside strings (e.g., `"Hello %name%"`)

---

## 1. Variables — Core Variable Store

**File:** `PLang/App/Memory/Variables.cs`

Thread-safe `ConcurrentDictionary<string, Data>` keyed by variable name (case-insensitive).

### Simple lookup: `%name%`

```csharp
public Data? Get(string name)
{
    name = CleanName(name);   // strips whitespace and % markers
    var rootName = GetRootName(name);  // "name" → "name"
    _variables.TryGetValue(rootName, out var root);
    return root;  // returns the Data wrapper
}
```

### Dot notation: `%user.name%`

```csharp
public Data? Get(string name)
{
    name = CleanName(name);   // "user.name"
    var rootName = GetRootName(name);  // "user"
    var remaining = name[(rootName.Length + 1)..];  // "name"

    _variables.TryGetValue(rootName, out var root);  // get the "user" Data
    return root.GetChild(remaining);  // navigate to .name
}
```

`GetRootName` splits at the first `.` or `[`:
- `"user.name"` → root = `"user"`, remaining = `"name"`
- `"addresses[0].street"` → root = `"addresses"`, remaining = `"[0].street"`
- `"name"` → root = `"name"`, remaining = null

### Indexed access: `%user.addresses[0].street%`

The `[0]` is handled by `Data.GetChild()` recursively, which delegates to `ValueNavigators`.

### Variable-in-index: `%user.addresses[idx].street%`

Before any lookup, `ResolveVariablesInPath` replaces variable names inside brackets:

```csharp
private string ResolveVariablesInPath(string path)
{
    // "addresses[idx].street" with idx=1 → "addresses[1].street"
    return Regex.Replace(path, @"\[([^\]\d][^\]]*)\]", match =>
    {
        var varName = match.Groups[1].Value;
        var resolved = GetValue(varName);
        return resolved != null ? $"[{resolved}]" : match.Value;
    });
}
```

---

## 2. Data.GetChild() — Property Navigation

**File:** `PLang/App/Memory/Data.cs`

When Variables finds the root variable, it calls `Data.GetChild(remaining)` to navigate the path. This is recursive:

```csharp
public Data? GetChild(string path)
{
    // Split path at first . or [
    // e.g. "name" → segment="name", remaining=""
    // e.g. "addresses[0].street" → segment="addresses", remaining="[0].street"
    // e.g. "[0].street" → segment="0", remaining="street"

    var childValue = GetChildValue(segment);  // delegate to navigators
    var child = new Data(segment, childValue, parent: this);

    if (string.IsNullOrEmpty(remaining))
        return child;
    return child.GetChild(remaining);  // recurse
}
```

### ValueNavigators — How properties are found on values

**File:** `PLang/App/Memory/Navigators/ValueNavigators.cs`

A chain of navigators tried in priority order:

| Priority | Navigator | Handles |
|----------|-----------|---------|
| 1 | `DictionaryNavigator` | `IDictionary` — looks up by key |
| 2 | `ListNavigator` | `IList` — index access, also `.first`, `.last`, `.random`, `.count` |
| 3 | `JsonStringNavigator` | strings that look like JSON — parses and navigates |
| 4 | `ObjectNavigator` | Any CLR object — uses reflection to find properties |

**ObjectNavigator** (the fallback):
```csharp
public object? GetProperty(object value, string key)
{
    var prop = value.GetType().GetProperty(key,
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    return prop?.GetValue(value);
}
```

This is where `%user.name%` ultimately resolves — if `user` is a CLR object (Dictionary, POCO, etc.), the navigator chain finds the `name` property/key.

---

## 3. Source-Generated Lazy Params — `__Resolve<T>()`

**File:** `PLang.Generators/LazyParamsGenerator.cs`

When the app runs an action, it passes the raw parameters from the .pr file to the handler. The source generator creates code that resolves `%var%` references lazily (at property access time).

There are **two pipelines** in the source generator:

### Pipeline 1: `BaseClass<T>` handlers (older pattern)

Generates a `*__Generated` record that wraps the parameter list:

```csharp
// Generated for a handler like FileReadHandler : BaseClass<read>
public sealed record read__Generated : read
{
    private readonly List<Data> _data;
    private readonly Variables _memory;

    public override string Path
    {
        get
        {
            var d = FindData("Path");
            return d != null ? Resolve<string>("Path")! : base.Path;
        }
    }

    private T? Resolve<T>(string name)
    {
        var data = FindData(name);
        if (data?.Value is string str && str.Contains('%'))
        {
            // Full match: "%user.name%" → get from memory, convert to T
            var fullMatch = Regex.Match(str, @"^%([^%]+)%$");
            if (fullMatch.Success)
                return (T?)TypeMapping.ConvertTo(
                    _memory.GetValue(fullMatch.Groups[1].Value), typeof(T));

            // Interpolation: "Hello %name%" → replace each %var%
            var interpolated = Regex.Replace(str, @"%([^%]+)%",
                m => FormatValue(_memory.GetValue(m.Groups[1].Value)));
            return (T?)TypeMapping.ConvertTo(interpolated, typeof(T));
        }
        return (T?)TypeMapping.ConvertTo(data?.Value, typeof(T));
    }
}
```

**Key point:** Resolution happens via regex at runtime:
- `^%([^%]+)%$` — full match (entire value is a single variable reference)
- `%([^%]+)%` — interpolation (variable mixed with literal text)

In both cases, `_memory.GetValue(varName)` is called, which goes through `Variables.Get()` → `Data.GetChild()` → `ValueNavigators`.

### Pipeline 2: `[Action]` classes (newer pattern)

Same resolution logic but structured differently — `__Resolve<T>` is a method on the generated partial class:

```csharp
partial class SomeAction : ICodeGenerated
{
    private List<Data>? __parameters;
    private Variables? __memoryStack;

    public partial string Content
    {
        get => __Resolve<string>("content")!;
        init { ... }
    }

    private T? __Resolve<T>(string name)
    {
        // Same regex-based resolution as Pipeline 1
        var data = __parameters?.FirstOrDefault(...);
        if (data?.Value is string str && str.Contains('%'))
        {
            // Full match or interpolation...
            // calls __memoryStack!.GetValue(varName)
        }
        return (T?)TypeMapping.ConvertTo(data?.Value, typeof(T));
    }
}
```

---

## 4. TString — String Interpolation

**File:** `PLang/App/Memory/TString.cs`

Used for translatable strings. Resolves `%var%` in a template string:

```csharp
internal static string Resolve(string template, Func<string, object?> resolver)
{
    // Scan for %...% pairs
    // For each pair, call resolver(varName)
    // If resolved → substitute
    // If not resolved → keep %varName% as-is
}
```

The resolver function is backed by Variables at runtime:
```csharp
new TString("Hello %name%", resolver: varName => memoryStack.GetValue(varName))
```

---

## Complete Resolution Flow: `%user.name%`

Here's the full path when a parameter value contains `%user.name%`:

```
.pr file: { "name": "Content", "value": "%user.name%", "type": "string" }
    ↓
Source-generated code (lazy param):
    property access triggers __Resolve<string>("content")
    ↓
Finds parameter data, value is string "%user.name%"
    ↓
Regex: ^%([^%]+)%$ matches → captured group = "user.name"
    ↓
__memoryStack.GetValue("user.name")
    ↓
Variables.Get("user.name"):
    CleanName → "user.name"
    GetRootName → "user"
    remaining → "name"
    _variables["user"] → Data { Value = { name: "john", age: 20, ... } }
    ↓
Data.GetChild("name"):
    segment = "name", remaining = ""
    GetChildValue("name") → ValueNavigators.Navigate(value, "name")
    ↓
ValueNavigators:
    DictionaryNavigator.CanNavigate? → YES (it's a Dictionary<string,object?>)
    DictionaryNavigator.GetProperty("name") → "john"
    ↓
Returns Data { Name="name", Value="john" }
    ↓
Back in __Resolve: TypeMapping.ConvertTo("john", typeof(string)) → "john"
    ↓
Action handler receives Content = "john"
```

## Current .pr Parameter Format

Parameters in the .pr file are simple objects with name/value/type:

```json
{
  "name": "Content",
  "value": "%user.name%",
  "type": "string"
}
```

The `value` is a raw string. Variable resolution (`%...%`) happens entirely at runtime via regex matching in the source-generated code. There is **no pre-parsed structure** — the runtime must parse the `%...%` markers, extract the variable path, and navigate it every time the property is accessed.

## What's Missing Today (Motivation for Pipes)

1. **No method calls** — `%user.name.ToUpper()%` is not supported; the runtime has no way to know `.ToUpper()` is a method vs a property
2. **No action calls** — can't pipe through module actions inside a variable expression
3. **Runtime parsing overhead** — regex runs on every property access; pre-parsing at build time would be faster
4. **No extensibility** — the resolution chain is hardcoded (Variables → Data.GetChild → Navigators); can't plug in new resolution strategies
