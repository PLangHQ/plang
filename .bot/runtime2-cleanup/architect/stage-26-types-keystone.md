# Stage 26: `types-keystone` (combined keystone)

**Read first:**
- `plan/principles.md` — OBP discipline. Rule C (static fields are a missing `@this`); the Context principle (objects navigate, they don't hold direct refs).
- `Documentation/Runtime2/good_to_know.md` — App's place as the bootstrap root; the Context navigation chain.
- Current state: `App/Types/this.cs` is already a delegating wrapper over `Utils/TypeMapping`. The wrapper exists *because* TypeMapping is static — Types is what the rest of App reaches for, but the implementation lives in static helpers.

**Goal:** Collapse three static surfaces (`Utils/TypeMapping.cs`, `Utils/PlangTypeIndex.cs`, `App/Choices/this.cs`) into one instance-shaped `app.Types` subsystem. Three folders/files vanish; `Types/this.cs` becomes the implementation (not a delegating wrapper); a new `Types/Registry.cs` partial absorbs PlangTypeIndex's internals; a new `Types/Choices/this.cs` sub-`@this` absorbs the [Choices] machinery in its OBP-correct home (under Types, not at root).

This is the Tier 5 keystone. Once it lands, the static-caller chain that blocked stages 16's deferred sites flattens; stage 27 (Utils empty-out) becomes mechanical.

## Why combined (was three separate stages)

The original Tier 5 plan had three separate stages: TypeMapping → instance keystone (stage 27), Choices eviction (stage 26), and Utils cleanup tail (stage 28). Walking the code while preparing the Choices brief surfaced the dependency:

- `TypeMapping.GetValidValues(type, context)` calls `Choices.Get(type, context)` — direct dependency.
- `GetValidValues` is itself called with `context = null` from 4 of 5 sites today.
- Evicting Choices alone would either need Context threaded through TypeMapping (a heavier change than the Choices stage was scoped for) or wait for TypeMapping itself to be instance-bound first.

They're one realignment: "the type subsystem becomes instance, with sub-types under `app.Types`." Doing them together also lets Choices land at its correct home (`app.Types.Choices`), avoiding a temporary mount at `app.Choices` that root-mounts a small build-time registry.

## Scope

**Included:**

1. **Public API surface moves from `TypeMapping` (static class) to `Types.@this` (instance).** All public methods of TypeMapping become public methods of Types, called via `app.Types.X` or `context.App.Types.X`. The delegating wrappers in today's `Types/this.cs` (lines 20, 51, 56, 70, 76, 82) become the implementation directly.

2. **`PlangTypeIndex` absorbs into a partial of `Types.@this`.** New file `App/Types/Registry.cs` declares `public partial class @this` and holds the 6 static fields (now instance), the lazy-init machinery, the assembly-walk indexing, and the public methods (`ResolveName`, `ResolveType`, `KnownTypes`, `RegisterRuntime`, `IsClrTypeName`). `PlangTypeIndex.cs` is deleted.

3. **`Choices` relocates under Types as a sub-`@this`.** New folder `App/Types/Choices/` with `this.cs`. Class is no longer static; `_gate` and `_registry` become instance fields; methods become instance. Mounted on `Types.@this` as `Choices` property. Old `App/Choices/` folder deleted.

4. **`app.Types` mount stays where it is** on `App.@this` — the property already exists. The shape inside changes; the mount path doesn't.

5. **Caller sweep across PLang/ + PLang.Tests/** — 21 files touch the affected symbols. Every `App.Utils.TypeMapping.X` → `app.Types.X` (or `context.App.Types.X` where context is in scope). Every `PlangTypeIndex.X` → `app.Types.X` (the surface flattens — Registry partial is internal-shape, public API is on `Types.@this`). Every `App.Choices.@this.X` → `app.Types.Choices.X`.

**Excluded:**
- `Utils/TypeConverter.cs` → `Types/Conversion.cs` partial — that's stage 27 (Utils empty-out).
- `Utils/Json.cs` dispersal — stage 27.
- Any change to behaviour of type resolution, name lookup, conversion, or [Choices] semantics. Pure shape change.
- Adding new API to Types beyond what's moved.
- Renaming any method (`GetType` → `Clr`, etc. — keep the names that exist on TypeMapping today; coder can flag rename candidates as follow-up).

## Deliverables

### New shape under `App/Types/`

```
App/Types/
├── this.cs              REWORKED — primary partial; absorbs TypeMapping public API
├── Registry.cs          NEW — partial; absorbs PlangTypeIndex internals
└── Choices/
    └── this.cs          NEW (relocated) — absorbs App/Choices/this.cs (instance, not static)
```

### Public API on `Types.@this` (after stage 26)

The methods below all become instance methods. Names match what TypeMapping exposes today; today's `Types/this.cs` delegating wrappers (`Clr`, `Name`, `Register`, `BuilderNames`, `ComplexSchemas`, `ValidValues`) get their bodies inlined from the corresponding TypeMapping static.

| Method (after) | Was on | Surface |
|----------------|--------|---------|
| `Type? Get(string plangName)` | TypeMapping.GetType | name → CLR |
| `Type? Clr(string plangName)` | Types.Clr (delegate) | name → CLR (alias for Get; today's Types method) |
| `Type? Clr(string mimeType)` overload | Types.ClrFromMime (static today) | MIME → CLR (rename **deferred** — keep ClrFromMime as second method; renaming is its own stage) |
| `string Name(Type clrType)` | TypeMapping.GetTypeName | CLR → name |
| `void Register(string, Type)` | TypeMapping.Register | register builder name |
| `string[]? ValidValues(Type, Context?)` | TypeMapping.GetValidValues | enum names + [Choices] vocab |
| `bool IsScalarPlangType(Type)` | TypeMapping.IsScalarPlangType | type kind |
| `bool IsPrimitive(Type)` | TypeMapping.IsPrimitive | type kind |
| `T? ConvertTo<T>(object?)` | TypeMapping.ConvertTo (delegates to TypeConverter) | conversion |
| `object? ConvertTo(object?, Type)` | TypeMapping.ConvertTo | conversion |
| `void Populate(object, IDictionary)` | TypeMapping.Populate | conversion |
| `(object? Value, Error? Error) TryConvertTo(object?, Type, Context?)` | TypeMapping.TryConvertTo | conversion |
| `List<string> GetBuilderTypeNames()` | TypeMapping.GetBuilderTypeNames | catalog support |
| `List<Schema.Entry> BuildTypeEntries(Modules.@this?)` | TypeMapping.BuildTypeEntries | catalog support |
| `string? ResolveName(Type)` | PlangTypeIndex.ResolveName | type → name reverse |
| `Type? ResolveType(string)` | PlangTypeIndex.ResolveType | name → type reverse (overlaps with Get?) |
| `IEnumerable<Type> KnownTypes()` | PlangTypeIndex.KnownTypes | type enumeration |
| `void RegisterRuntime(string, Type)` | PlangTypeIndex.RegisterRuntime | dynamic registration |
| `bool IsClrTypeName(string?)` | PlangTypeIndex.IsClrTypeName | CLR-type detection |
| `Choices.@this Choices` | App.Choices.@this | sub-property |

**Note for the coder:** `Get(string)` and `ResolveType(string)` look like they overlap. Read both bodies — if they do the same thing, consolidate as one method (`Get` is the existing public name on TypeMapping). If they differ in some way (depth recursion, registry vs runtime fallback), keep both with clear docs. Either choice is fine; this brief leaves it to the implementation read.

**Note on `ClrFromMime`:** today static on `Types.@this:28`. The plan post-cleanup-tree had it as a `Clr(mimeType)` overload of the name-lookup method. Don't rename in this stage — keeping both `Clr(plangName)` and `ClrFromMime(mimeType)` separate is fine; method-overload by parameter name (both take `string`) would be ambiguous. If a rename is wanted, it's a separate stage's call.

### `Types/Registry.cs` partial

```csharp
namespace App.Types;

public sealed partial class @this
{
    // The 6 fields PlangTypeIndex held statically — now instance:
    private readonly object _initLock = new();
    private bool _initialized;
    private readonly ConcurrentDictionary<string, Type> _nameToType = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Type, string> _typeToName = new();
    private readonly ConcurrentDictionary<string, Type> _runtimeNameToType = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _clrTypeFullNames = new(StringComparer.Ordinal);
    private volatile bool _clrTypeFullNamesInitialized;
    private readonly object _clrTypeFullNamesLock = new();

    /// <summary>Assemblies indexed for [PlangType] discovery.</summary>
    public List<Assembly> Assemblies { get; } = new() { typeof(@this).Assembly };

    public bool IsClrTypeName(string? name) { ... }
    public string? ResolveName(Type type) { ... }
    public Type? ResolveType(string name) { ... }
    public IEnumerable<Type> KnownTypes() { ... }
    public void RegisterRuntime(string name, Type type) { ... }

    private void EnsureInitialized() { ... }
    private void EnsureClrTypeFullNamesInitialized() { ... }
    private void IndexAssembly(Assembly assembly) { ... }
    private static bool IsThisClass(Type type) { ... }   // pure logic, can stay static
    private static string? InferName(Type type) { ... }   // pure logic, can stay static
}
```

The `_initLock`, dictionaries, and `_initialized` flag move from static to instance — Rule C closed for those fields. The two pure-logic helpers (`IsThisClass`, `InferName`) stay `static` because they're behaviour, not state, and per Rule C's exception list (static factory methods and helpers are fine).

`typeof(@this).Assembly` replaces `typeof(PlangTypeIndex).Assembly` for the bootstrap assembly seed.

### `Types/Choices/this.cs`

```csharp
namespace App.Types.Choices;

public sealed class @this
{
    private readonly object _gate = new();
    private Dictionary<System.Type, MethodInfo>? _registry;

    public string[]? Get(System.Type type, Actor.Context.@this? context = null) { ... }
    public bool Has(System.Type type) { ... }

    private System.Type Unwrap(System.Type type) { ... }
    private Dictionary<System.Type, MethodInfo> EnsureRegistry() { ... }
}
```

Body unchanged from today's `App/Choices/this.cs`; only the `static` keyword drops at every level (class, fields, methods).

Mount in `Types/this.cs`:
```csharp
public Choices.@this Choices { get; } = new();
```

### Mounting on App

`App/this.cs` already declares `Types`. The current declaration likely reads `public global::App.Types.@this Types { get; } = new();` — leave it alone. The shape change is internal to `Types.@this`; the mount is unchanged.

### Caller propagation (21 files)

Mechanical sweep. Three transformation rules:

1. `App.Utils.TypeMapping.X(...)` → `<navigation>.Types.X(...)` where `<navigation>` is `app`, `context.App`, `_context.App`, or whatever's locally available.
2. `PlangTypeIndex.X(...)` → `<navigation>.Types.X(...)` (same — the partial absorbs it; public surface is on `Types.@this`).
3. `App.Choices.@this.X(...)` → `<navigation>.Types.Choices.X(...)`.

**Where Context isn't available in the caller:**

A handful of call sites are themselves static helpers that today reach the static `TypeMapping`. Identify and decide per site:

- **`App/Types/this.cs:82`** — `public static string[]? ValidValues(System.Type type) => Utils.TypeMapping.GetValidValues(type);`. This static helper goes away; the public API moves to instance `Types.ValidValues`. Callers that had `Types.ValidValues(type)` (statically) need to navigate to an instance — through `app.Types.ValidValues(type)` or similar.
- **`App/Utils/TypeMapping.cs:407`** — `var values = GetValidValues(type);` (internal recursion, currently no Context). Once `GetValidValues` is instance, this internal call becomes `this.GetValidValues(type)` — Context flows through if the method is called with one; if not, the [Choices] path falls back to `Choices.Get(type, null)` as today.
- **`PLang/App/modules/builder/validateResponse.cs:245`** — handler has `IContext`; navigate via `Context.App.Types.ValidValues(targetType)`.
- **Two test sites** in `TypeMappingTests.cs` and `ComplexTypeDiscoveryTests.cs` — these construct an App fixture; navigate via `app.Types.ValidValues(...)`.

Coder discretion on the static-helper-to-instance transitions. The brief leaves the per-site decision to the implementer because each call site has its own context-availability story; the architect's call is "the realignment is correct, the tactical edits are mechanical."

### Definition of done

- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2752/2752).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --tester` green from a fresh rebuild.
- `find PLang/App/Utils/TypeMapping.cs PLang/App/Utils/PlangTypeIndex.cs` — both gone.
- `find PLang/App/Choices` — directory absent.
- `find PLang/App/Types/Registry.cs PLang/App/Types/Choices/this.cs` — both present.
- `grep -rn "App\.Utils\.TypeMapping\b\|PlangTypeIndex\b\|App\.Choices\.@this" PLang/ PLang.Tests/ --include='*.cs'` — zero hits.
- `grep -n "public static class TypeMapping\|public static class PlangTypeIndex\|public static class @this" PLang/App/Types/ PLang/App/Utils/` — zero hits in the type subsystem (other static classes elsewhere are not affected by this stage).
- `grep -n "private static" PLang/App/Types/this.cs PLang/App/Types/Registry.cs PLang/App/Types/Choices/this.cs` — only pure-logic-helper exceptions (e.g. `IsThisClass`, `InferName`).

**Dependencies:**

- **Stage 25 (HTTP Default statics)** — independent, can land in either order.
- **Stage 27 (Utils empty-out)** — must come *after* this stage. Stage 27's `TypeConverter` move depends on `Types.@this` being a partial (which this stage establishes). Stage 27's `Utils.Json` dispersal also benefits from the Types spine being settled first.
- **Stage 24 (Callback Wire)** — independent.

## Design

### The smell this closes

Three Rule C sites under one realignment:

1. **`Utils/TypeMapping.cs`** is `public static class TypeMapping` with two static dictionaries (`Primitives`, `PrimitiveNames`) plus a flat surface of static methods. Not just Rule C on the fields — the whole class shape is a god-bag of "type stuff" that has no `@this`. Today's `Types/this.cs` is a delegating wrapper *because* TypeMapping is static; the wrapper exists to give callers a `app.Types.X` navigation point that resolves to a static under the hood.

2. **`Utils/PlangTypeIndex.cs`** is `public static class PlangTypeIndex` with 6 static fields including locks and lazy-init guards. Process-global indexing state — exactly Rule C's target. The lazy init pattern (`_initLock` + `_initialized` + `EnsureInitialized`) is itself the Rule C smell: data with no owner that needs lazy guards because there's no constructor to put it in.

3. **`App/Choices/this.cs`** is `public static class @this` (the static `@this` shape — already a contradiction; `@this` is the OBP convention for instances). Two static fields (`_gate`, `_registry`), one with lazy-init via lock-double-check.

All three close the same way: hand the data to an `@this` that constructs it. `Types.@this` becomes that owner. The locks disappear (or stay as instance lock objects per the Rule C exception, depending on remaining concurrency needs after construction). The lazy-init flags either disappear (initialise eagerly in the ctor) or stay as instance flags (defer if the indexing is expensive and not always needed).

### Why one combined stage, not three

Stages 16 and the original Tier 5 had these as three separate cleanups. The reason it works as one combined stage:

- **They share callers.** TypeMapping calls Choices. PlangTypeIndex is internal to TypeMapping (TypeMapping.GetTypeName line 211 calls PlangTypeIndex.ResolveName). Splitting the eviction means doing each in isolation while the other two stay static — and the static-to-instance transition is most painful exactly when the callers are themselves static (which is what Choices's blocker was).

- **They share an owner.** All three are pieces of the type subsystem. There's no honest stopping point at "TypeMapping is now instance but PlangTypeIndex is still static" — TypeMapping calls into PlangTypeIndex directly (line 211, line 145, etc.); both have to move together.

- **They share the spine.** Once `Types.@this` is the canonical type subsystem, the three pieces become partials/sub-types under it. Three separate stages would have created intermediate states where Types.@this delegates to one static, owns one partial, and exposes another sub-type — a scaffolded shape that's harder to reason about than the end state.

The cost of combining: the stage is bigger (a session, possibly two). The benefit: each intermediate state is coherent — Types either owns the implementation (after) or delegates to TypeMapping (before). No half-migrated middle.

### Why Choices under Types, not at root

Three observations push the move:

1. **Choices is a per-type registry.** Every operation is "given a Type, do something with its [Choices] method." Type is the dimension. The sub-property reading `app.Types.Choices.Get(type)` is honest about what the registry indexes by.

2. **Types already has a `ValidValues(type)` surface.** Today that delegates through TypeMapping → Choices. After this stage, `Types.ValidValues(type)` either delegates to `Choices.Get(type)` directly (one layer) or absorbs the [Choices] path inline. Either is one-step navigation; today's path is three-layer.

3. **Choices is build-time-only.** `App/Choices/`'s docstring is explicit: "Build-time validator and catalog Describe() both go through here." Root mount overstates its weight — it reads as "Choices is a top-tier subsystem like Goals, Channels, Modules" when it's actually a small implementation detail of how the build-time validator confirms LLM-emitted vocab. Under Types it's clearly "the type subsystem's vocabulary lookup."

The original plan's tentative move was `Builder/Choices/`. Builder is honest about the build-time-only use, but it's about the build *process* (snapshot, RunAsync) — Choices is a *registry* keyed by Type. Types is a stronger semantic match.

### What about `Types/Conversion.cs`?

The plan post-cleanup-tree mentions `Types/Conversion.cs` as a partial absorbing `TypeConverter`. **That is stage 27, not stage 26.** Reason for splitting:

- `Types/Registry.cs` (this stage) — absorbs PlangTypeIndex; the registry-indexing concern.
- `Types/Conversion.cs` (stage 27) — absorbs TypeConverter; the value-conversion concern.

The two concerns are independent enough that they can land in two stages without reopening the Types primary. After stage 26: Types has `this.cs` + `Registry.cs` + `Choices/`. After stage 27: Types adds `Conversion.cs`. After both: Utils/ ends up at the four files originally specified.

### Initialization ordering

`Types.@this` is constructed during App boot. Its sub-types — `Choices.@this` (field-init `= new()`) and the `Registry` partial's instance state — initialise as part of Types construction. Today's lazy-init in PlangTypeIndex (`_initLock` + `_initialized`) was needed *because* there was no constructor — process-static fields can't run setup. Once Types.@this owns the data, the indexing can either:

- **(a) Run in the ctor** — eager init. Simpler; one source of truth; no lock needed (the ctor is single-threaded). Cost: indexing the assembly happens at every App allocation; in tests with many fixtures, that's measurable. But it's the same cost today on first access; just shifted earlier.
- **(b) Stay lazy with instance lock** — preserves today's deferred indexing. Lock object becomes instance, flag becomes instance, behaviour identical. Slightly more code; same cost.

**Architect's lean: (b)**, because it preserves behaviour exactly and tests already exercise the lazy path. Coder's call if the eager init turns out simpler in practice.

## Risk + dependencies

**Risk: medium.** The keystone touches 21 files and reshapes a public API surface. The behaviour is preserved everywhere, but the volume of caller edits creates surface for typos. Compiler catches everything that's syntactic; runtime tests catch everything that's wired-up wrong.

Possible failure modes:

1. **A static caller chain we missed.** The grep above lists 21 files; sweep before claiming done — `grep -rn "App\.Utils\.TypeMapping\|PlangTypeIndex\|App\.Choices\.@this"` should be zero. If a hit remains, the call site needs Context to navigate to `app.Types`; that may surface a deeper static-helper that needs threading — flag and decide per case.
2. **Assembly-walk ordering.** PlangTypeIndex's `Assemblies` list is populated by various subsystems before lookup. Confirm the ordering still works when the list is instance — the seed is `typeof(@this).Assembly`; downstream additions need a way to reach `app.Types.Assemblies` (or whatever the property name becomes).
3. **The `_clrTypeFullNames` cache.** This caches all CLR type FullNames — static today, populated lazily, thread-safe via a separate lock. Per-instance allocation moves the cache to per-App lifetime. In test parallelism that allocates many Apps, the cache rebuilds for each. If this is a hot path in tests, profile and consider whether the cache deserves a different scope (process-static `const`-style data, or shared between Apps via a separate static class — but the latter reopens Rule C).
4. **The source generator.** `LazyParamsGenerator.cs:638` once referenced `App.Utils.*` symbols (per `plan/post-cleanup-tree.md`'s v3 absorption note). That reference was removed — confirmed by grep — so the source generator should be unaffected. Re-verify with a fresh build.
5. **Tests that construct TypeMapping behaviour without an App.** The unit tests in `PLang.Tests/App/Utility/TypeMappingTests.cs` either construct an App fixture or call statics directly. After this stage, all calls go through an `app.Types` instance; tests that call `TypeMapping.X` statically need to switch to constructing a test App and navigating. **Confirm test setup pattern early in the stage** to avoid late surprise.

**Dependencies:**
- **None upstream.** Independent of stages 23, 24, 25.
- **Stage 27 depends on this.** TypeConverter → Types/Conversion.cs partial can't land until Types is partial (this stage).

## Watch for (coder eyes-on)

- **The combined size.** This is the largest Tier 5 stage. Plan for two sessions if the first runs out of context — the natural breakpoint is "Types.@this owns the public API and Registry partial" (end of session 1) → "Choices relocates and callers sweep" (session 2).
- **Don't rename methods.** TypeMapping's surface uses `GetType`, `GetTypeName`, `GetValidValues`, etc. Today's `Types.@this` delegating wrapper renamed some (`Clr`, `Name`, `ValidValues`). Both naming styles exist in the wild. Pick whichever your local logic prefers — but the brief's principle is "preserve behaviour and shape; renames are their own follow-up." If you hit a place where the same method name has two callers expecting different things, surface it (probably means there's a `Get(string)` vs `ResolveType(string)` distinction worth keeping).
- **`ClrFromMime` vs `Clr` overload.** The post-cleanup-tree had Clr taking a `string` and disambiguating by content. Both `Clr(plangName)` and `Clr(mimeType)` would have ambiguous signatures. Keep them separate (`Clr` for plang names, `ClrFromMime` for MIME) until/unless a different naming scheme is settled.
- **Static helpers vs instance methods.** `IsThisClass(Type)`, `InferName(Type)`, `StripGenericArity(string)`, `UnwrapType(Type)` — pure-logic helpers with no state access. **Keep them `static private`** — Rule C exception #1 (pure logic with no state). Only state-touching methods become instance.
- **`Types/this.cs:82` — `public static string[]? ValidValues(...)`** — this is the existing static helper that callers already use. Becomes instance. Sites calling `Types.ValidValues(t)` need to switch to `app.Types.ValidValues(t)` — sweep with `grep -n "Types\.ValidValues\|Types\.@this\.ValidValues"`.
- **The order of partial declarations.** Make `this.cs` declare `public sealed partial class @this`. `Registry.cs` declares `public sealed partial class @this`. Compiler is order-insensitive but consistent placement helps reviewers.
- **`Choices.@this.Get(type, context)`** — the call signature is unchanged; what changes is that the receiver is now an instance reached via `app.Types.Choices` instead of the static `App.Choices.@this`.

## Out of scope

- `Utils/TypeConverter.cs` movement — stage 27.
- `Utils/Json.cs` dispersal — stage 27.
- Any `Types.@this` API rename (e.g. unifying `Get` and `ResolveType`) — settle in a follow-up if needed.
- Any change to source-generator-emitted type metadata.
- Any change to MIME-table behaviour or `Formats/this.cs` — that's stable from stage 18.
- Any reshape of `Types/this.cs:80` documentation to mention the new partials — XML doc cleanup is fine but not required.

## Commit plan

```
runtime2-cleanup stage 26: Types keystone — TypeMapping + PlangTypeIndex + Choices

Three static surfaces (Utils/TypeMapping.cs, Utils/PlangTypeIndex.cs,
App/Choices/this.cs) collapsed into one instance-shaped app.Types
subsystem. Tier 5 keystone — unblocks stage 27.

Shape after:
- App/Types/this.cs       — REWORKED. Primary partial. Absorbs TypeMapping
                             public API (GetType, GetTypeName, ValidValues,
                             ConvertTo, IsScalarPlangType, etc.). Was a
                             delegating wrapper; now the implementation.
- App/Types/Registry.cs   — NEW. Partial. Absorbs PlangTypeIndex internals
                             (assembly indexing, name/type dictionaries,
                             lazy-init, IsClrTypeName, ResolveName/Type,
                             KnownTypes, RegisterRuntime). Static fields
                             become instance fields; lazy init stays
                             behind an instance lock.
- App/Types/Choices/      — NEW (relocated from App/Choices/). Sub-@this
   └── this.cs              under Types. Static class becomes instance;
                             two static fields become instance fields.
                             Mounted as app.Types.Choices.

Files deleted:
- App/Utils/TypeMapping.cs
- App/Utils/PlangTypeIndex.cs
- App/Choices/ (whole folder)

Caller sweep (21 files): App.Utils.TypeMapping.X → app.Types.X (or
context.App.Types.X); PlangTypeIndex.X → app.Types.X (public surface
on the partial); App.Choices.@this.X → app.Types.Choices.X.

Behaviour preserved everywhere — pure shape change. Three Rule C
sites closed in one realignment. The static-caller chain that
blocked Choices's separate eviction unwinds because all three
move together.

C# 2752/2752 + PLang 199/199 baseline preserved.

Tier 5 stage 26 (combined keystone, was stages 26+27 in the
original plan). Stage 27 (Utils empty-out — TypeConverter →
Types/Conversion.cs partial; Utils/Json disperse) follows.
```
