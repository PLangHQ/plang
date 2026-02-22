# Phase 2 Implementation Plan: Type Context + Lazy Derivation

## Goal

Type becomes a navigator — lazily reaches Engine.Types through context for Kind, Compressible, ClrType. Data gets a late-bound context property. Type derivation becomes lazy (no eager creation on Value set).

## Changes

### 1. Type gets context + lazy navigation properties

```csharp
public sealed class Type
{
    public string Value { get; }
    [JsonIgnore] internal PLangContext? Context { get; set; }

    // Navigate lazily through context
    public string? Kind => Context?.Engine.Types.KindOf(Value);
    public bool Compressible => Kind != null && (Context?.Engine.Types.Compressible(Kind) ?? false);
    public System.Type? ClrType => Context?.Engine.Types.Clr(Value) ?? TypeMapping.GetType(Value);
    // ClrType falls back to static TypeMapping when contextless (static Type.String, tests, etc.)
}
```

Serialization unchanged — TypeJsonConverter only reads/writes Value string.
Static convenience properties (`Type.String`, `Type.Int`) are contextless — Kind/Compressible return null/false.

### 2. Engine.Types gets `KindOf(string typeValue)` method

Current `Kind(extension)` maps file extensions → kind. But `Type.Value` is a PLang type name or MIME, not an extension. Need a new method:

```csharp
public string? KindOf(string typeValue)
{
    // If it's already a known kind (e.g. "image", "encrypted"), return it
    if (_allKinds.Contains(typeValue)) return typeValue;
    // If it looks like a MIME type, look up via reverse map
    if (typeValue.Contains('/') && _mimeToKind.TryGetValue(typeValue, out var kind)) return kind;
    return null;
}
```

Requires:
- `_allKinds`: `HashSet<string>` of all known kinds (built from `_extensionToKind.Values`)
- `_mimeToKind`: `Dictionary<string, string>` reverse map from MIME → kind (built from _extensionToKind + _extensionToMime)

### 3. Data gets late-bound context

```csharp
public class Data
{
    private PLangContext? _context;

    [JsonIgnore]
    public PLangContext? Context
    {
        get => _context;
        set
        {
            _context = value;
            if (_type != null) _type.Context = value;
        }
    }
}
```

### 4. Type derivation becomes lazy

**Constructor**: `_type = type;` — store explicit type if given, null otherwise. No eager derivation.

**Value setter**: `_type = null;` — invalidate cached type, don't re-derive.

**Type getter**: lazily derive when accessed and _type is null:
```csharp
get {
    if (_type != null) return _type;
    if (_value == null) return null;
    var derived = _context?.Engine.Types != null
        ? new Type(Context.Engine.Types.Name(_value.GetType()))
        : new Type(TypeMapping.GetTypeName(_value.GetType()));
    derived.Context = _context;
    _type = derived;
    return _type;
}
```

### 5. Context stamping points

Where Data gets context set:

| Point | File | How |
|-------|------|-----|
| MemoryStack.Set | MemoryStack.cs | MemoryStack gets `Context` property, stamps on new/existing Data |
| MemoryStack.Put | MemoryStack.cs | Same |
| Action.RunAsync | Action/Methods.cs | Stamp context on result Data before return variable binding |
| Data.GetChild | Data.cs | Child inherits context from parent |
| PLangContext.RegisterContextVariables | PLangContext.cs | Stamp `this` on Data/DynamicData created there |
| PLangContext constructor | PLangContext.cs | Set `MemoryStack.Context = this` |

**Data.Ok() / Data.FromError()** — static factories create contextless Data (unchanged). Context stamped later by whoever receives it.

### 6. MemoryStack gets context reference

```csharp
public class MemoryStack
{
    [JsonIgnore] internal PLangContext? Context { get; set; }
}
```

PLangContext constructor sets `MemoryStack.Context = this` after creating the MemoryStack.
MemoryStack.Clone() creates a new MemoryStack without context — PLangContext.CreateChild stamps context on the child's MemoryStack via its constructor.

## File Changes Summary

| File | Action | What |
|------|--------|------|
| `PLang/Runtime2/Engine/Memory/Data.cs` | **Modify** | Add Context property to Data, lazy Type derivation, child context inheritance |
| `PLang/Runtime2/Engine/Memory/MemoryStack.cs` | **Modify** | Add Context property, stamp on Set/Put |
| `PLang/Runtime2/Engine/Types/this.cs` | **Modify** | Add KindOf(), _allKinds, _mimeToKind |
| `PLang/Runtime2/Engine/Context/PLangContext.cs` | **Modify** | Set MemoryStack.Context, stamp on context variables |
| `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/Action/Methods.cs` | **Modify** | Stamp context on result Data |
| `PLang.Tests/Runtime2/Types/EngineTypesTests.cs` | **Modify** | Add KindOf tests |
| `PLang.Tests/Runtime2/Memory/DataTests.cs` | **Modify or Create** | Test lazy derivation, context stamping, context inheritance |

## What is NOT changing

- TypeJsonConverter — serialization unchanged
- Static TypeMapping — still exists, used as fallback for contextless Type
- Static factories (Data.Ok, Data.FromError) — create contextless Data
- DynamicData — inherits context from MemoryStack.Put stamping
- Data<T> — inherits context behavior from base Data
