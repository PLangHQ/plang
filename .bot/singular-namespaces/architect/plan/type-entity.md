# Type entity — `type.@this` and `data.Type`

The biggest and riskiest piece. It is here because the accessor model demands it: `app.type["int"]` is only worth the keystrokes if what comes back is *an object you work with*. Today it would be a raw `System.Type`, with the actual per-type knowledge scattered. This stage consolidates that knowledge into a real entity.

## The problem: "a type" is smeared across three places today

- **`System.Type`** — the CLR type (`types/this.cs` `Get(string)`, `Clr(string)` return this).
- **`builder.Types.Entry`** (`builder/Types/Entry.cs`) — the rich per-type description: `Name, Kind, Fields, Values, Shape, ConstructorSignature, Example, Description, ClrType`. Built on demand for the LLM via `BuildTypeEntries`.
- **name + scheme strings** — `GetTypeName`, `Name`, `Scheme`.

So there is no single object representing one PLang type. `app.type["int"]` returning any one of these alone is half a thing.

## The promotion

`type.@this` becomes the entity for **one PLang type**, owning its own knowledge:

```
app.type["int"]      → type.@this   (name "int", clr typeof(int), scheme, valid-values, …)
app.type.of<int>()   → type.@this   (same, selected by CLR type)
app.type.list        → enumerate
data.Type            → type.@this   the value's type — context.app.type[Value]
```

`type.list.@this` (folder `type/list/`) becomes the registry; the current `types/this.cs` selection/conversion methods (`Get`, `Clr`, `GetTypeName`, `Name`, `Register`, `RegisterDomainTypes`) reshape into: the registry does selection (`[name]`, `of<T>()`) + lifecycle (`Register`), and per-type knowledge (name, clr, scheme, valid-values, the conversion the type knows how to do) moves onto `type.@this`. The `Choices` and `Scheme` sub-registries (`type/choice/`, `type/path/scheme/`) stay where they are, reached through the type or the registry as today.

`builder.Types.Entry` is the existing rich shape — the entity absorbs what it carries (Fields/Shape/Example/Description become the type's own, or the builder reads them off the entity). The builder schema path (`BuildTypeEntries`, `ComplexSchemas`, `builder/Types/Render.cs`) reshapes to read from `type.@this` instead of constructing a parallel `Entry`. **This is the part that reshapes behavior** — treat the builder schema rendering as the integration risk and pin it with tests (see test-coverage.md).

## `data.Type` is the natural home

Every value is a `data`; a type is never free-floating, it is always something a value *has*. So the entity's primary door is `data.Type`, not `app.type[...]`:

```csharp
// data/this.cs — was ClrType returning System.Type with a static fallback
public type.@this Type => context.app.type[Value];   // context + app non-null (stage 2); registry holds primitives
```

`app.type[...]` is the registry door (used by the loader, the builder, conversion); `data.Type` is the door you hold. Both return the same entity.

## What the 80 `Types` call sites become

| Today | After |
|---|---|
| `app.Types.Get("int")` / `.Clr(name)` (8) | `app.type[name]` (→ `.clr` if the CLR type is what's wanted) |
| `app.Types.GetTypeName(runtimeType)` / `.Name(t)` (6) | `app.type[t].name` (reverse selection by `System.Type` + `.name` on the entity) |
| `app.Types.GetValidValues(t)` (1) | `app.type[t].validValues` |
| `app.Types.IsClrTypeName(name)` (3) | `app.type.contains(name)` or `app.type[name] != null` |
| `app.Types.Scheme.*` (5), `.Choices.*` (2) | unchanged sub-registries, reached via `app.type.scheme` / `app.type.choice` |
| `app.Types.Spec.*` (19), `.Entry`, `.EntryKind`, `.Field` (12) | these are the builder schema shape — reshape to read off `type.@this`; the heaviest part |
| `app.Types.BuildTypeEntries` / `ComplexSchemas` / `GetBuilderTypeNames` | reshape onto the entity collection |

`of<T>()` (the compile-time generic) has no current caller — every site is a runtime string or reflected `System.Type`. Provide it for ergonomics, but the load-bearing operations are `[name]` (string→entity), `[Type]` (reflected→entity), and `.name`/`.clr` on the entity. Don't over-invest in `of<T>()`.

## Scope and sequencing

- Depends on **stage 2** (non-null `context`, so `data.Type => context.app.type[...]` has no fallback) and **stage 3** (the `app.type` accessor exists).
- This is the one stage that genuinely *moves logic* (the brief's original §7 scoped it out; Ingi pulled it in). Keep the registry's selection/lifecycle clean and put every per-type behavior on the entity, same rule as everywhere else.
- If it proves too large to land safely with the rest, it is the natural cut point to split back out — but the goal is all four in one branch. Flag to Ingi if the builder schema reshape balloons.
