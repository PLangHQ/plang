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

### What Becomes Data\<T\> — Everything

Everything is Data. No exceptions.

| Type | Data\<T\> | Property collisions (use `new`) |
|------|----------|--------------------------------|
| Path (was PathData) | `Data<Path>` | Path (file path vs navigation path) |
| Identity (was IdentityData) | `Data<Identity>` | — |
| Settings (was SettingsData) | `Data<Settings>` | — |
| Engine | `Data<Engine>` | Name |
| Goal | `Data<Goal>` | Name, Path |
| Step | `Data<Step>` | — |
| Action | `Data<Action>` | — |
| Future domain types | `Data<T>` | Name, Type, Path as needed |

All `!` system variables (`%!goal%`, `%!step%`, `%!engine%`, etc.) are DynamicData on MemoryStack — factories that compute the current value at time of access. Since all runtime types are now `Data<T>`, the factory returns typed Data directly. `%!goal.Name%` navigates via DeclaredOnly to Goal.Name = "Start". `%!goal!Name%` reaches Data.Name = "!goal".

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

### Source Generator — `Data()` Accessor and `GetValue<T>()` Conversion

The source generator creates a `Data(string)` method that gives the handler access to the underlying Data for any parameter. No companion properties — one method covers all parameters:

```csharp
// Source generator creates:
private Dictionary<string, Data> __paramData;
protected Data Data(string paramName) => __paramData[paramName];

// Generated ExecuteAsync — no __TryConvert, Data converts itself:
__paramData = new(StringComparer.OrdinalIgnoreCase);

var __sizeData = __memoryStack.Get("size");
__paramData["Size"] = __sizeData;
Size = __sizeData.GetValue<long>();       // Data knows how to convert to long

var __pathData = __memoryStack.Get("filePath");
__paramData["FilePath"] = __pathData;
FilePath = __pathData.GetValue<Path>();   // Path.From(string, context) kicks in
```

Handler code:
```csharp
public Task<Data> Run()
{
    var sizeData = Data(nameof(Size));              // clean, compile-time safe via nameof
    if (!sizeData.Success) return Task.FromResult(sizeData);  // error passthrough

    // normal work with Size (the long value)
    // ...
}
```

**What goes away:** `__TryConvert<T>` helper, `Size__Source` companion properties. The conversion logic lives on Data (`GetValue<T>()`) and on target types (`From`), not duplicated in generated code. The source generator simplifies to: get from MemoryStack, store in dictionary, call `GetValue<T>()`.

For domain types that ARE Data (like Path), `Data(nameof(FilePath))` works but is unnecessary — `FilePath.Error`, `FilePath.Success` are already accessible directly.

### Type Conversion — Target Owns It

When `Data<string>` needs to become a `Path` (e.g., handler declares `Path FilePath` but MemoryStack holds a string), the **target type** owns the conversion. This is OBP applied to type conversion — behavior belongs to the owner.

The conversion point is `GetValue<T>()` in Data:

```csharp
public T? GetValue<T>()
{
    if (Value is T already) return already;

    // Does T know how to create itself from this value?
    // Mechanism TBD — static From() as placeholder, will evolve
    var fromMethod = typeof(T).GetMethod("From", ...);
    if (fromMethod != null) return (T)fromMethod.Invoke(null, new[] { Value, Context });

    // Fallback: primitive conversions only (string→long, int→double, etc.)
    return TypeMapping.TryConvertTo<T>(Value);
}
```

Each type declares what it can convert from:

```csharp
public class Path : Data<Path>
{
    public static Path From(string raw, PLangContext context) => new Path(raw, context);
    // string → Path: resolve the raw path against context's filesystem
}

public class Identity : Data<Identity>
{
    public static Identity From(string publicKey, PLangContext context) => ...;
    // string → Identity: lookup by public key
}
```

**TypeMapping.TryConvertTo simplifies dramatically** — it becomes a small lookup table for primitive conversions (`string→long`, `int→double`, `string→bool`). All interesting conversions (string→Path, string→Identity, dict→User) live on the target type. The Swiss army knife becomes a utility drawer.

**Note:** The static `From` convention is a placeholder. The principle — target type owns conversion — is the architectural decision. The exact mechanism (static method, interface, registered converter) will be refined during implementation.

**The movie for parameter injection:**
1. PLang developer writes: `- read file.txt, write to %content%` then `- file.read %content%`
2. Step 1 stores `Data<string>` with Value = "./readme.md"
3. Step 2's handler declares `Path FilePath`
4. Source generator calls `GetValue<Path>()` on the Data<string>
5. `Value is Path` → false
6. `Path.From("./readme.md", context)` → resolves to absolute path, returns Path
7. Handler receives a proper Path with Exists, Size, Extension — not a raw string

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

### Phase 3: Engine + Entities (high impact)
- `Engine : Data<Engine>` — the root becomes Data, `ms.Put(this)` instead of wrapping
- `Goal : Data<Goal>` — `new` on Name and Path, system variable `%!goal%` stored directly
- `Step : Data<Step>` — system variable `%!step%` stored directly
- `Action : Data<Action>` — accessible via `%!step.Actions%`
- All use DeclaredOnly navigation via registered navigators

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
