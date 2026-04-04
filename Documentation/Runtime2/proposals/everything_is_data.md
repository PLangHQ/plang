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

**After** (everything is Data — polymorphic navigation):

The simplification isn't "one code path" — it's "each type owns its own navigation." `GetChildValue` becomes virtual. No priority chain, no whitelist — each Data type knows how to traverse its content.

```csharp
// Data base class — default implementation
protected virtual object? GetChildValue(string key)
{
    // 1. Own properties via reflection (works for strongly-typed Data<T> subclasses)
    var ownProp = GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
    if (ownProp != null) return ownProp.GetValue(this);
    
    // 2. Properties collection (extensible metadata)
    var prop = Properties[key];
    if (prop != null) return prop.Value;
    
    return null;
}
```

Types override when reflection isn't the right strategy:

```csharp
// Data wrapping a JSON/dictionary value — key lookup, not reflection
protected override object? GetChildValue(string key)
{
    if (Value is IDictionary<string, object?> dict)
        return dict.TryGetValue(key, out var v) ? v : null;
    return base.GetChildValue(key);
}

// Data wrapping a list — index access
protected override object? GetChildValue(string key)
{
    if (Value is IList list && int.TryParse(key, out var idx) && idx < list.Count)
        return list[idx];
    return base.GetChildValue(key);
}

// Path : Data<Path> — no override needed, reflection finds Exists, Size, Extension
// Engine : Data<Engine> — no override needed, reflection finds Goals, FileSystem, etc.
// Identity : Data<Identity> — no override needed, reflection finds PublicKey, IsDefault, etc.
```

Strongly-typed `Data<T>` subclasses get correct navigation for free via the base reflection. Only plain Data wrapping untyped content (JSON dicts, lists) needs an override. The current fragile priority chain (check Value? check subclass? check whitelist? check ValueNavigators?) collapses into: **each type handles itself.**

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

PLang is strongly typed — all action parameters have concrete types (`long`, `string`, `Path`, `User`, etc.). No `object?` parameters. The source generator resolves each parameter from MemoryStack to its declared type.

**Before:**
```csharp
// Handler declares: public partial long Size { get; init; }
// Generator resolves %size% from MemoryStack:
var __resolved = __memoryStack!.Get("size");       // returns Data wrapper
Size = __TryConvert<long>(__resolved?.Value);       // extract .Value → loses Data
```

**After:**
```csharp
// Same handler declaration: public partial long Size { get; init; }
// Generator resolves %size% from MemoryStack:
var __data = __memoryStack!.Get("size");            // returns Data (which may BE the value)
Size = __TryConvert<long>(__data);                   // converts Data → long
Size__Source = __data;                               // keeps reference to the Data
```

The key change: `__TryConvert<T>` knows how to extract from Data. For primitives (`long`, `string`, `bool`), it pulls from `Data.Value`. For domain types (`Path`, `Identity`, `User`), the Data IS the type — `__TryConvert<Path>` returns the Data directly since `Path : Data<Path>`. No `.Value` extraction needed.

### 6. Handler ergonomics — __Source companion

All parameters are strongly typed at the handler level. The developer writes `long Size`, `Path FilePath`, `Identity Signer` — concrete types, not Data. But sometimes the handler needs the Data metadata (error state, properties).

The generator emits a companion for each parameter:
```csharp
// Generated
public partial long Size { get; init; }          // the strongly-typed value
internal Data? Size__Source { get; private set; } // the Data it came from

public partial Path FilePath { get; init; }      // Path IS Data<Path>, already has metadata
// No __Source needed — FilePath.Error, FilePath.Properties already accessible

// In ExecuteAsync:
var __sizeData = __memoryStack.Get("size");
Size__Source = __sizeData;
Size = __TryConvert<long>(__sizeData);
```

For domain types extending `Data<T>`, no `__Source` companion is needed — the parameter itself IS Data. `FilePath.Error`, `FilePath.Properties`, `FilePath.Success` are directly accessible. `__Source` is only needed for primitive parameters where the Data wrapper would otherwise be lost.

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

### Name — one property, one purpose
Subclasses (Engine, Goal, Step, etc.) drop their own `Name` property. `Data.Name` is the single identity — it's how the object is found on the MemoryStack.

- `Data.Name = "!engine"` → found via `%!engine%` (system variable, `!` is part of the name)
- `Data.Name = "Start"` → found via `%goal%` when stored as goal, name used for goal resolution
- `Data.Name = "myEngine"` → `create new engine, write to %myEngine%` → name is "myEngine"

The name IS the MemoryStack key. No "variable name" vs "display name" — one field. For Engine : Data<Engine>, the old `Engine.Name = "Runtime2"` is removed — it was never meaningful. The system engine is `"!engine"`, a user-created engine would be `"myEngine"`.

For navigation:
- `%!engine.Goals%` → navigates to Engine's Goals property (`.` navigates own properties since Value = this via CRTP)
- `%!engine!Name%` → Data metadata access → `"!engine"`
- `%user.name%` → navigates Value (the JSON dict) → `"john"` (Value ≠ this, so `.` navigates the dict)
- `%user!Name%` → Data metadata → `"user"`

The `.` vs `!` distinction only matters when Value holds something different from the Data itself (e.g., plain Data wrapping JSON). For domain types where Value = this (CRTP), `.` and `!` reach the same properties.

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
