# Proposal: Everything is Data

## Summary

`Data<T>` is the universal base for all values in PLang's runtime. Objects that participate in the value system — stored in variables, returned from handlers, navigated by PLang developers — inherit from `Data<T>` instead of being wrapped in Data. Navigation is split into two conventions: `.` for domain properties, `!` for Data infrastructure. Each type's navigation is handled by a registered navigator, not by inheritance or priority chains.

## Design Principles

1. **If it flows through the value system, it IS Data.** No wrapping, no two-tier system.
2. **Navigation is a registered capability, not an inheritance artifact.** Each type has a navigator registered on the engine.
3. **`.` = domain, `!` = infrastructure.** Two rules, deterministic, no priority chain.
4. **Errors are self-describing.** The Error object carries its own context. The container doesn't distinguish error sources.
5. **Program structure stays separate.** Goal, Step, Action are the program — they don't participate in the value system.

## Current Architecture

```
Data (base)
├── Value: object?          ← the actual content
├── Name, Type, Properties  ← metadata
├── Error, Success, Handled ← result state
├── Navigation (GetChild)   ← dot-path traversal into Value
│
├── IdentityData : Data     ← PublicKey, PrivateKey
├── PathData : Data          ← Exists, Size, Extension
├── DynamicData : Data       ← lazy Value via factory
├── DataList<T> : Data       ← typed list with error state
└── Data<T> : Data           ← generic typed wrapper (CRTP: where T : Data<T>)
```

**Problems:**
- Engine, Path, Identity get wrapped in Data when stored on MemoryStack — artificial
- GetChildValue has a fragile 5-step priority chain (check Value-as-Data, check subclass props, check Properties, check ValueNavigators, check whitelisted base props)
- DataList, DataDict, DataJson — special classes that don't end
- Handlers must wrap domain objects in Data before returning — but handlers are one line (`return await provider.Run(data)`)
- Data.Name collides with domain property names (User.Name, Document.Name)

## Proposed Architecture

### Data\<T\> — Universal Base, No CRTP

```csharp
public class Data<T> : Data  // no constraint — T is whatever the content shape is
{
    public new T? Value
    {
        get => base.Value is T typed ? typed : GetValue<T>();
        set => base.Value = value;
    }
}
```

No `where T : Data<T>` constraint. This allows two usage patterns:

**Domain types — T is self, Value returns this:**
```csharp
public class User : Data<User>
{
    public new string Name { get; set; }   // "John Johnson" — hides Data.Name
    public string Email { get; set; }
}

public sealed class @this : Data<@this>, IAsyncDisposable  // Engine
{
    public EngineGoals Goals => _goals;
    public IPLangFileSystem FileSystem { get; set; }
}
```

**Wrapping types — T is the content:**
```csharp
new Data<List<string>>("items", myList)    // was DataList<string>
new Data<long>("count", 42)                // primitive
new Data<Dictionary<string, object?>>("config", dict)  // was special-cased
```

No DataList, DataDict, DataJson. Just `Data<T>` where T describes the shape. The type parameter IS the description.

### Navigation: `.` vs `!`

Two conventions, two code paths, no ambiguity:

**`.` (dot) — domain properties:**
Uses DeclaredOnly reflection on the concrete type. If the property isn't declared on the domain type, checks Properties collection. If nothing, returns empty Data. Never falls through to inherited Data properties.

```
%user.Name%     → DeclaredOnly on User → "John Johnson"
%user.Email%    → DeclaredOnly on User → "john@example.com"
%user.Foo%      → not found → Properties["Foo"] → empty Data
%user.Error%    → not declared on User → empty Data (NOT Data.Error)
```

**`!` (bang) — Data infrastructure:**
Full hierarchy reflection. Reaches Data's own properties — Name, Type, Error, Success, Properties, etc.

```
%user!Name%     → Data.Name → "user" (the variable binding)
%user!Type%     → Data.Type → PLang type descriptor
%user!Error%    → Data.Error → error state if any
%user!Success%  → Data.Success → bool
```

The `!` convention telegraphs "I'm reaching into plumbing." Most developers never use it. But for debugging, error handling, and metaprogramming, it's always there.

### Registered Navigators

Navigation is NOT baked into GetChildValue via if/else chains. Each type has a navigator registered on the engine:

```csharp
// In GetChildValue (which delegates to the navigator)
protected override object? GetChildValue(string key)
{
    var navigator = Engine.Navigators.Get(typeof(T));
    return navigator?.Navigate(this, key);
}
```

The runtime ships with navigators for common types:

| Type | Navigator behavior |
|------|-------------------|
| Domain types (User, Engine, Path) | DeclaredOnly reflection — finds typed properties |
| `List<T>` | Index access, `.first`, `.last`, `.count` |
| `Dictionary<string, object?>` | Key lookup (case-insensitive) |
| `Json` | JSON key/path navigation |
| CLR object (fallback) | Reflection on public properties |

Module authors register navigators for their types. Third parties can register their own. The navigator registry is on the engine — `engine.Navigators.Register<T>(navigator)`.

This separates two concerns:
- **What the object is** → `Data<T>`, the type
- **How to traverse it** → navigator, registered on the engine

You can change how navigation works for a type without touching the type. New types get navigation by registering a navigator, not by overriding a virtual method.

### Property Collisions — the `new` Keyword

Data has properties that collide with common domain names: `Name`, `Type`, `Path`, `Value`, `Parent`. Domain types that need these names use C#'s `new` keyword:

```csharp
public class User : Data<User>
{
    public new string Name { get; set; }   // hides Data.Name
    public new string Type { get; set; }   // hides Data.Type (if needed)
}
```

This is safe because:
- **Navigation uses DeclaredOnly** — `.` finds User.Name ("John Johnson"), never Data.Name ("user")
- **MemoryStack accesses the base** — `((Data)user).Name` gets the variable key. MemoryStack always works with the Data base layer.
- **`!` reaches infrastructure** — `%user!Name%` explicitly accesses Data.Name

The `new` keyword is only needed for colliding names. Most domain properties (Email, PublicKey, Exists, Size, Goals) don't collide. And the collision-prone names (Name, Type, Path) are the minority.

### What Becomes Data\<T\> — Scoping

**YES — value system participants:**

| Type | Why |
|------|-----|
| Path (was PathData) | Created by handlers, stored in variables, navigated |
| Identity (was IdentityData) | Created by handlers, stored in variables |
| Settings (was SettingsData) | Stored in variables, navigated |
| Engine | Root runtime object, may be created/stored by PLang developers |
| Future domain types (User, Message, etc.) | Any handler-created object that flows through variables |

**NO — program structure:**

| Type | Why |
|------|-----|
| Goal | The program itself. Users call goals, don't store them in variables. |
| Step | Internal execution unit. Never flows through the value system. |
| Action | Internal execution unit. Dispatch target, not a value. |

The system variables `%!step%` and `%!goal%` exist for debugging. They continue to use the current wrapping approach (`new Data("!step", step)`) — this is system registration code, not a handler, so the one-line constraint doesn't apply.

### Handler Ergonomics — One Line

The one-line handler constraint is a forcing function for this design. Handlers are:

```csharp
public async Task<Data> Run() => await provider.Run(this);
```

If Engine IS Data, the provider returns the engine directly. No wrapping. The type system handles it — `Data<Engine>` is assignable to `Data`. The provider creates a domain object and returns it. Done.

### MemoryStack Storage

```csharp
// Before — wrapping
ms.Put(new Data("!engine", engine));

// After — direct
ms.Put(engine);  // Engine IS Data, Key set by caller via Data.Name
```

### Source Generator — `__Source` Companion

Independent of the Data<T> hierarchy, the source generator adds a `__Source` companion for primitive parameters:

```csharp
// Generated
public partial long Size { get; init; }          // the strongly-typed value
internal Data? Size__Source { get; private set; } // the Data it came from

// For domain types extending Data<T>, no __Source needed:
public partial Path FilePath { get; init; }      // Path IS Data — .Error, .Properties accessible
```

`__TryConvert<T>` knows: for primitives, extract from Data.Value. For domain types, the Data IS the type — return it directly.

## Migration Strategy

### Phase 1: Rename + Navigator Registry (low risk)
- Rename `PathData` → `Path : Data<Path>` (already extends Data)
- Rename `IdentityData` → `Identity : Data<Identity>` (already extends Data)
- Rename `SettingsData` → `Settings : Data<Settings>` (already extends Data)
- Build the navigator registry on the engine
- Register navigators for List, Dictionary, Json, CLR reflection
- Update GlobalUsings, all references

### Phase 2: Navigation Split (medium risk)
- Implement `.` vs `!` parsing in GetChild
- `.` delegates to registered navigator (DeclaredOnly for domain types)
- `!` accesses Data base properties directly
- Empty Data on miss (no fallback)
- Remove the GetChildValue priority chain
- Remove ValueNavigators static class (replaced by registry)

### Phase 3: Engine (high impact)
- `Engine : Data<Engine>` — the root becomes a value system participant
- `ms.Put(this)` instead of wrapping
- Navigation works via registered navigator

### Phase 4: Source Generator
- Add `__Source` companion for primitive parameters
- `__TryConvert<T>` handles Data<T> domain types directly
- Remove `.Value` extraction for resolved Data objects

### Phase 5: Remove Special Classes
- Remove `DataList<T>` — use `Data<List<T>>` with list navigator
- Remove any DataDict, DataJson if they exist
- Plain `Data` remains as the abstract base for result factories (`Data.Ok()`, `Data.FromError()`)

## Risks and Considerations

### Property Hiding with `new`
The `new` keyword creates two storage slots — `((Data)user).Name` and `((User)user).Name` are different values. This is intentional: Data.Name is the variable binding ("user"), User.Name is the domain value ("John Johnson"). MemoryStack and serialization always work with the Data base. Navigation uses DeclaredOnly on the concrete type. The two slots serve two purposes and never cross.

### Serialization
Data has `[JsonPropertyName("name")]` and `[JsonPropertyName("value")]`. When Engine extends Data, serialization includes these. `[JsonIgnore]` and `TransportPropertyFilter` must correctly handle the deeper inheritance. The `new` keyword properties need explicit `[JsonPropertyName]` if they participate in serialization.

### Constructor Chains
Every domain class constructor must call `base(name)`. The `name` parameter is the default variable binding. For Engine: `base("!engine")`. For Path: `base(rawPath)`. For handler-created types, the runtime sets Data.Name from the `write to %varName%` instruction after construction.

### Clone/Copy Family
Data has `virtual Clone()`. Every new subclass must override it correctly. This is a known bug pattern — the audit must cover all copy methods (constructor, Clone, CreateChild, factory methods, deserialization).

### Performance
Data carries Properties, Type, Context, Error, Warnings. For Engine (one instance) this is nothing. For Path (many instances during file operations), measure. The overhead should be negligible since these fields are initialized lazily or null by default.

### Navigator Registration Timing
Navigators are registered on the engine. Types created before the engine exists (during bootstrap) need a fallback — likely the CLR reflection navigator as a default. The registry must handle this gracefully.

## Summary

`Data<T>` is the universal base for PLang values. No CRTP constraint — T describes the content shape. `.` navigates domain properties via DeclaredOnly reflection, `!` reaches Data infrastructure. Navigation is pluggable per-type via registered navigators on the engine. No special DataList/DataDict classes — just `Data<List<T>>`, `Data<Dictionary<...>>`. Domain types use `new` for the rare property name collision. Goal, Step, Action stay as program structure. Everything that flows through the value system IS Data.
