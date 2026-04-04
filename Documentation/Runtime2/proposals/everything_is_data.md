# Proposal: Everything is Data

## Summary

Make `Data` the universal base class for all PLang runtime objects. Instead of Data wrapping objects via `Value`, objects ARE Data — they inherit from it and carry navigation, error handling, properties, and metadata natively.

## Current Architecture

```
Data (base)
├── Value: object?          ← the actual content (Engine, dict, string, etc.)
├── Name, Type, Properties  ← metadata
├── Error, Success, Handled ← result state
├── Navigation (GetChild)   ← dot-path traversal into Value
│
├── IdentityData : Data     ← PublicKey, PrivateKey as own properties
├── PathData : Data          ← Exists, Size, Extension as own properties  
├── DynamicData : Data       ← lazy Value via factory
├── DataList<T> : Data       ← typed list with error state
└── Data<T> : Data           ← generic typed wrapper
```

**Engine**, **Goal**, **Step**, **Action** — standalone classes, NOT Data. They get wrapped in Data when stored on MemoryStack:

```csharp
ms.Put(new Data("!engine", Engine));  // Engine wrapped in Data.Value
```

**The problem**: Navigation must choose between Data's own properties and Value's properties. `%user.name%` → is it `Data.Name` or `Value.name`? Current fix: priority ordering in `GetChildValue()`. Fragile, already caused bugs.

## Proposed Architecture

```
Data (base)
├── Name, Type, Properties, Error, Success, Handled
├── Navigation (GetChild) — navigates THIS object's properties
│
├── Engine : Data            ← Id, Name, Goals, FileSystem, etc.
├── Goal : Data              ← Name, Steps, Path, Parent
├── Step : Data              ← Index, Text, Actions
├── Action : Data            ← Module, ActionName, Parameters
├── Path : Data              ← Exists, Size, Extension (was PathData)
├── Identity : Data          ← PublicKey, PrivateKey (was IdentityData)
├── DynamicData : Data       ← lazy value computation
├── DataList<T> : Data       ← typed list
└── Data<T> : Data           ← generic typed access
```

No more `Value` as the primary content holder for domain objects. The object IS the content.

## What Changes

### 1. Domain classes extend Data

**Before:**
```csharp
public sealed class @this : IAsyncDisposable  // Engine
{
    public string Id { get; }
    public string Name { get; set; }
    public EngineGoals Goals => _goals;
    public IPLangFileSystem FileSystem { get; set; }
    // ...
}
```

**After:**
```csharp
public sealed class @this : Data, IAsyncDisposable  // Engine
{
    public @this(string absolutePath, ...) : base("engine")
    {
        // Data properties: Name = "engine", no Value to set — this IS the value
    }
    
    public string Id { get; }
    // Name already on Data — Engine.Name IS Data.Name
    public EngineGoals Goals => _goals;
    public IPLangFileSystem FileSystem { get; set; }
    // ...
}
```

**Navigation just works:**
```
%!engine.Goals%        → Engine.Goals (own property)
%!engine.Name%         → Engine.Name (own property, no ambiguity)
%!engine.Success%      → Data.Success (inherited)
%!engine.Error%        → Data.Error (inherited)
%!engine.Properties%   → Data.Properties (inherited)
```

### 2. Rename: drop "Data" suffix

| Before | After | Reason |
|--------|-------|--------|
| `PathData` | `Path` | It IS a path, the `: Data` is the pattern |
| `IdentityData` | `Identity` | Same |
| `SettingsData` | `Settings` | Same |
| `SignedData` | `Signed` or `Signature` | Same |

### 3. GetChildValue simplifies

**Before** (current — fragile priority ordering):
```csharp
private object? GetChildValue(string key)
{
    var val = Value;
    if (val is Data dataVal) { /* check dataVal properties */ }
    
    // Subclass properties
    var ownProp = GetType().GetProperty(key);
    if (ownProp != null && ownProp.DeclaringType != typeof(Data)) return ownProp.GetValue(this);
    
    // Data.Properties collection
    var prop = Properties[key];
    if (prop != null) return prop.Value;
    
    // Navigate Value object via reflection ← this is the fragile part
    if (val != null) return ValueNavigators.Navigate(val, key);
    
    // Whitelisted Data base properties (Name, Success, Error)
    if (ownProp != null && IsWhitelisted(key)) return ownProp.GetValue(this);
    
    return null;
}
```

**After** (everything is Data):
```csharp
private object? GetChildValue(string key)
{
    // 1. Own properties (subclass + Data base — ALL are valid, no whitelist needed)
    var ownProp = GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    if (ownProp != null) return ownProp.GetValue(this);
    
    // 2. Properties collection (extensible metadata)
    var prop = Properties[key];
    if (prop != null) return prop.Value;
    
    // 3. Value navigation (for Data objects that still wrap plain values — strings, dicts, etc.)
    if (Value != null && Value != this) return ValueNavigators.Navigate(Value, key);
    
    return null;
}
```

No more priority ordering. No more whitelist. Own properties always win because there's no `Value` competing with them.

### 4. MemoryStack storage

**Before:**
```csharp
ms.Put(new Data("!engine", Engine));           // Engine wrapped in Data.Value
ms.Put(new Data("MyIdentity", identityData));  // IdentityData IS Data already
```

**After:**
```csharp
ms.Put(Engine);      // Engine IS Data, Name = "engine" → stored directly
ms.Put(identity);    // Identity IS Data, Name = "MyIdentity" → stored directly
```

MemoryStack.Put takes Data directly. No wrapping.

### 5. Source generator — parameter resolution

**Before:**
```csharp
var __resolved = __memoryStack!.Get("result");     // returns Data wrapper
return __TryConvert<T>(__resolved?.Value, name);    // extract .Value → loses Data
```

**After:**
```csharp
var __resolved = __memoryStack!.Get("result");      // returns the object (which IS Data)
return __TryConvert<T>(__resolved, name);            // object IS the value, no extraction
```

For primitive parameters (long, string, bool), `__TryConvert<T>` converts from the Data's typed properties. For `object?` parameters, the Data object passes through directly.

### 6. Handler ergonomics — __Source companion

When the generator resolves `%size%` (a long) from MemoryStack, it extracts the primitive. The handler code works with `long Size`. But sometimes the handler needs the Data metadata (error, properties).

The generator emits a companion:
```csharp
// Generated
public partial long Size { get; init; }          // the primitive value
internal Data? Size__Source { get; private set; } // the Data it came from

// In ExecuteAsync:
Size__Source = __memoryStack.Get("size");
// Size is resolved from Size__Source via __TryConvert<long>
```

Handler code:
```csharp
public Task<Data> Run()
{
    var size = Size;                    // long — normal use
    var meta = Size__Source;            // Data — when you need metadata
    if (!meta.Success) return meta;     // error passthrough
    // ...
}
```

### 7. Data.Value — what happens to it?

`Data.Value` still exists but its role changes:

| Object type | Value behavior |
|-------------|---------------|
| `Engine : Data` | `Value` returns `this` (the engine IS the value) |
| `Path : Data` | `Value` holds file content when read, null otherwise |
| `Identity : Data` | `Value` returns `this` |
| Plain `Data("x", 42)` | `Value` = 42 (wraps primitives, dicts, lists) |
| `DynamicData` | `Value` = factory() result |

For domain objects that ARE Data, `Value` defaults to `this`. For plain Data wrapping primitives, `Value` holds the primitive. The `ToString()` override on each type controls string representation.

Override pattern:
```csharp
public class Path : Data
{
    // Value = file content (after read) or null (before read)
    // Navigation: %path.Exists% → own property, %path.Value% → file content
    
    public override object? Value
    {
        get => base.Value;  // file content, or null
        set => base.Value = value;
    }
}

public class Engine : Data  
{
    // No Value override needed — Data.Value returns null by default
    // All properties are own properties, navigable directly
    // ToString() → Engine.Name
}
```

## Migration Strategy

### Phase 1: Leaf types (low risk)
- Rename `PathData` → `Path`, already extends Data ✓
- Rename `IdentityData` → `Identity`, already extends Data ✓
- Rename `SettingsData` → `Settings`, already extends Data ✓
- Update GlobalUsings, all references

### Phase 2: Entity types (medium risk)
- `Goal : Data` — Name and Path already exist on both, need reconciliation
- `Step : Data` — Index, Text become navigable
- `Action : Data` — Module, ActionName become navigable
- Each gains Error, Success, Properties, Handled for free

### Phase 3: Engine (high impact)
- `Engine : Data` — the root becomes navigable
- `%!engine.Name%` works without special registration
- MemoryStack registration simplifies — Engine puts itself: `ms.Put(this)`

### Phase 4: Source generator update
- Remove `.Value` extraction for resolved Data objects
- Add `__Source` companion properties
- Simplify `GetChildValue` — remove whitelist and priority chain

### Phase 5: Navigation cleanup
- Remove `ValueNavigators.Navigate` fallback for types that ARE Data
- Keep it for plain `Data` wrapping dicts, lists, primitives

## Risks and Considerations

### Name collision: Data.Name vs Goal.Name vs Engine.Name
Currently `Data.Name` is the variable name ("!engine"), while `Engine.Name` is the engine name ("Runtime2"). When Engine extends Data, these become the same property. Resolution: `Data.Name` stores the variable name, domain-specific names use different property names if needed, or accept that the variable name IS the domain name (e.g., Goal name = variable name).

### Serialization
Data has `[JsonPropertyName("name")]` and `[JsonPropertyName("value")]` on its properties. When Engine extends Data, Engine serialization includes these. Need to verify that serialization modifiers (`[JsonIgnore]`, `[Sensitive]`, `TransportPropertyFilter`) work correctly with the deeper inheritance.

### Constructor chains
Every domain class constructor must call `base(name)`. For existing Data subclasses this already works. For Engine/Goal/Step, constructors need refactoring to thread through the Data constructor.

### Clone/Copy
Data has `virtual Clone()`. Every new subclass must override it correctly (the clone/copy family audit pattern). This is already a known bug pattern — extending it to more classes increases the surface.

### Performance
Data carries Properties (dictionary), Type (lazy), Context (reference), Error, Warnings. For high-frequency objects like Step or Action, this overhead may matter. Measure before assuming it's fine.

### Data<T> — does it still make sense?
`Data<T>` provides `new T? Value` with typed access. If objects ARE Data, do we still need `Data<T>`? Yes — for handler return types: `Task<Data<string>>` communicates the expected value type. But the pattern shifts: `Data<T>` wraps primitives, domain types extend Data directly.

## Summary

The core insight: **Data wrapping objects via Value creates an ambiguity layer** (whose property is it?). Making objects inherit Data removes that layer. Navigation becomes direct — own properties first, no priority chain, no whitelist.

The migration is incremental (leaf → entity → engine → generator). Each phase is independently valuable and testable. Phase 1 (renaming existing Data subclasses) is zero-risk. Phase 3 (Engine : Data) is the transformative step.
