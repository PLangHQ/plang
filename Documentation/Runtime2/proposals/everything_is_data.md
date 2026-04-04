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

**Navigation convention**: `%user.name%` always navigates the Value object's properties. `%user!name%` (with `!`) accesses Data's own metadata properties (Name, Success, Error, Properties). The `!` prefix distinguishes data-level access from value-level access. This is not ambiguous — the convention is clear. The current priority ordering bug in `GetChildValue()` was a code issue, not a design issue.

## Proposed Architecture

Everything extends `Data<T>` where `T` is itself — the CRTP (Curiously Recurring Template Pattern). This gives typed access to the object while inheriting all Data infrastructure.

```
Data (base)
├── Name, Type, Properties, Error, Success, Handled
├── Navigation (GetChild) — navigates THIS object's properties
│
Data<T> : Data               ← generic typed access, T? Value with typed getter
│
├── Engine : Data<Engine>     ← Id, Name, Goals, FileSystem, etc.
├── Goal : Data<Goal>         ← Name, Steps, Path, Parent
├── Step : Data<Step>         ← Index, Text, Actions
├── Action : Data<Action>     ← Module, ActionName, Parameters
├── Path : Data<Path>         ← Exists, Size, Extension (was PathData)
├── Identity : Data<Identity> ← PublicKey, PrivateKey (was IdentityData)
├── DynamicData : Data        ← lazy value computation (no typed self-ref needed)
└── DataList<T> : Data        ← typed list
```

No more `Value` as the primary content holder for domain objects. The object IS the content. `Data<T>` provides typed `Value` access — for domain types, `Value` returns `this`.

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
public sealed class @this : Data<@this>, IAsyncDisposable  // Engine : Data<Engine>
{
    public @this(string absolutePath, ...) : base("engine")
    {
        // Data properties: Name = "engine", Value returns this (CRTP)
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
| `Engine : Data<Engine>` | `Value` returns `this` (the engine IS the value, via CRTP) |
| `Path : Data<Path>` | `Value` holds file content when read, `this` otherwise |
| `Identity : Data<Identity>` | `Value` returns `this` |
| Plain `Data("x", 42)` | `Value` = 42 (wraps primitives, dicts, lists) |
| `DynamicData` | `Value` = factory() result |

For domain objects extending `Data<T>`, `Data<T>.Value` returns the typed self-reference via CRTP. For plain Data wrapping primitives, `Value` holds the primitive. The `ToString()` override on each type controls string representation.

Override pattern:
```csharp
public class Path : Data<Path>
{
    // Value = file content (after read) via base.Value
    // Data<Path>.Value returns this when base.Value is null
    // Navigation: %path.Exists% → own property, %path.Content% → file content
}

public class Engine : Data<Engine>
{
    // Data<Engine>.Value returns this (CRTP)
    // All properties are own properties, navigable directly
    // ToString() → Engine.Name
}
```

## Migration Strategy

### Phase 1: Leaf types (low risk)
- Rename `PathData` → `Path : Data<Path>`, already extends Data ✓
- Rename `IdentityData` → `Identity : Data<Identity>`, already extends Data ✓
- Rename `SettingsData` → `Settings : Data<Settings>`, already extends Data ✓
- Update GlobalUsings, all references

### Phase 2: Entity types (medium risk)
- `Goal : Data<Goal>` — Name and Path already exist on both, need reconciliation
- `Step : Data<Step>` — Index, Text become navigable
- `Action : Data<Action>` — Module, ActionName become navigable
- Each gains Error, Success, Properties, Handled for free

### Phase 3: Engine (high impact)
- `Engine : Data<Engine>` — the root becomes navigable
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
Currently `Data.Name` is the variable name ("!engine"), while `Engine.Name` is the engine name ("Runtime2"). When Engine extends Data<Engine>, these merge — `Name` serves both roles. The `!` navigation convention resolves access: `%engine.Name%` → the domain property (engine name), `%engine!Name%` → the Data metadata property (variable name). For Engine, these may be the same value. For cases where they differ (e.g., Goal named "Start" stored as variable "goal"), the `!` prefix provides explicit access to the variable name.

### Serialization
Data has `[JsonPropertyName("name")]` and `[JsonPropertyName("value")]` on its properties. When Engine extends Data, Engine serialization includes these. Need to verify that serialization modifiers (`[JsonIgnore]`, `[Sensitive]`, `TransportPropertyFilter`) work correctly with the deeper inheritance.

### Constructor chains
Every domain class constructor must call `base(name)`. For existing Data subclasses this already works. For Engine/Goal/Step, constructors need refactoring to thread through the Data constructor.

### Clone/Copy
Data has `virtual Clone()`. Every new subclass must override it correctly (the clone/copy family audit pattern). This is already a known bug pattern — extending it to more classes increases the surface.

### Performance
Data carries Properties (dictionary), Type (lazy), Context (reference), Error, Warnings. For high-frequency objects like Step or Action, this overhead may matter. Measure before assuming it's fine.

### Why inheritance, not an interface?

Considered `IData<T>` instead of `Data<T>` base class. Rejected because:

- **Data's value is its state** — Properties dictionary, Error, Handled, Navigation implementation, Clone, Context. An interface declares the contract but can't carry instance state. Every implementing class would duplicate 15+ fields and methods.
- **Default interface methods** (C# 8+) can't hold instance state. You'd need a backing field on each class anyway — that's what a base class provides.
- **Composition** (`Engine._data = new Data(...)`) creates the same wrapper gap — MemoryStack stores Data, and Engine isn't one. Navigation doesn't work transparently.
- **No inheritance conflict** — Engine, Goal, Step, Path, Identity don't extend any other class today. They implement interfaces (IAsyncDisposable, IList<T>) which are unaffected by a base class.

Inheritance is the right call. The state IS the point.

### Data<T> — CRTP constraint

`Data<T>` uses the Curiously Recurring Template Pattern with a constraint:

```csharp
public class Data<T> : Data where T : Data<T>
```

The `where T : Data<T>` constraint ensures you can only write `Engine : Data<Engine>`, never `Engine : Data<Foo>`. This is strict by design — each type's CRTP self-reference is enforced at compile time.

Domain types extend `Data<T>` where T is themselves: `Engine : Data<Engine>`, `Path : Data<Path>`. This gives:
- Typed `Value` property that returns `T?` (self-reference for domain types, primitive for wrappers)
- Typed `Ok(T value)` factory
- Typed `FromError(IError)` factory
- Handler return types: `Task<Data<string>>` for primitives, `Task<Data>` for general use

The current `Data<T>` needs a small change to support CRTP — when `T` is the subclass itself and `base.Value` is null, `Value` should return `(T)this`:
```csharp
public class Data<T> : Data where T : Data<T>
{
    public new T? Value
    {
        get => base.Value is T typed ? typed 
             : base.Value == null && this is T self ? self  // CRTP: self-reference
             : GetValue<T>();
        set => base.Value = value;
    }
}
```

## Summary

Making objects inherit `Data<T>` gives them navigation, error handling, properties, and metadata for free. The `!` navigation convention (`%obj.prop%` for value, `%obj!prop%` for Data metadata) eliminates ambiguity. The CRTP constraint enforces type safety at compile time.

The migration is incremental (leaf → entity → engine → generator). Each phase is independently valuable and testable. Phase 1 (renaming existing Data subclasses) is zero-risk. Phase 3 (Engine : Data<Engine>) is the transformative step.
