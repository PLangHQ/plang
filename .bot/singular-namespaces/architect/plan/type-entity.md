# Type entity — `type.@this` and `data.Type`

The biggest and riskiest piece. **plang-types already built a version of it** — this stage finishes the job by giving it the right home and the right ownership.

## What plang-types already shipped

`data.Type` no longer returns a bare `System.Type`. It returns a `type` object — `app.data.type`, a sealed class living *inside `app/data/this.cs`* — that owns `Value` (the PLang type name), `ClrType`, `Kind`, `Compressible`, and `Convert`, with static factories (`String`, `Int`, `FromName`, `FromMime`). The flat `data.ClrType` is gone; the CLR type is reached at **`data.Type.ClrType`**. So Stage 4's headline — *the type is an entity, the CLR type lives on it* — is **done**.

It landed with a different split than this stage originally drew:

- **`app.types.@this`** (`types/this.cs` + `Registry.cs` + `Conversion.cs`, ~650 lines) is the **registry/service** — name↔CLR resolution (`Get`/`Clr`/`GetTypeName`/`Name`), the builder catalog (`BuildTypeEntries`/`ComplexSchemas`), runtime DLL loading (`Loader.cs`), and four child registries: `Choices`, `Scheme` (`path.scheme`), `Kinds`, `Renderers`. It hands back bare `System.Type`.
- **`app.data.type`** is the **value-side descriptor** — the thing a value carries.

That's this branch's own rule (registry = selection + lifecycle; knowledge on the element) — just with the registry named `types` (the plural we're renaming) and the entity homed in `data` instead of `type`.

## The promotion (1B): unify under `type/`

The entity belongs in the `type` namespace, not `data`. So:

```
type/this.cs        → type.@this        the entity (moved out of app.data.type)
type/list/this.cs   → type.list.@this   the registry (demoted from app.types.@this)
```

- `data.Type` returns `type.@this`. `app.type[name]` returns `type.@this`. `app.type[System.Type]` (reverse) returns `type.@this`. Both doors, one entity.
- The registry `type.list.@this` keeps only **selection** (`[name]`, `[Type]`, `of<T>()`), **lifecycle** (`Register`, runtime DLL load via `Loader`), and the **catalog walk** (`BuildTypeEntries` — *enumerate types and project each to its schema*; the transitive discovery is a collection operation, so it stays here).
- The four child registries (`Choices`, `Scheme`, `Kinds`, `Renderers`) stay as children of the registry, reached through `app.type.choice` / `app.type.scheme` / `app.type.kinds` / `app.type.renderers` (apply the singular rule to the new folder names — `kinds`→`kind`, `renderers`→`renderer`, `primitives`→`primitive` — coder's call where a folder is genuinely a collection).
- The concrete value-type folders — `type/number/`, `type/image/`, `type/code/`, `type/datetime/`, `type/duration/`, `type/path/**` — ride along below `type/` unchanged. They are the CLR types a value *is*; `type.@this` is the meta-descriptor *about* a PLang type. Both live in the `app.type` namespace and don't collide (`app.type.number.@this` vs `app.type.@this`).

## Entry folds into the entity (decision 2)

`builder.Types.Entry` (`builder/Types/Entry.cs`, 12 use sites) is **not a separate concept** — it is the PLang type, described for the LLM. Every field on it is intrinsic to the type:

| Entry field | What it is on the type |
|---|---|
| `Name` | the PLang name |
| `Kind` (Record/Enum/Scalar) | the type's classification |
| `Fields` | a Record's constructable fields |
| `Values` | an Enum's values (= `ValidValues`) |
| `Properties` | a Scalar's navigable runtime properties |
| `Shape`, `ConstructorSignature` | a Scalar's wire form + `Resolve(...)` signature |
| `Example`, `Description`, `Kinds` | teaching metadata the type carries about itself |
| `ClrType` | the CLR type |

So `type.@this` absorbs all of it (computed lazily by reflecting over its `ClrType`). The builder **renders** from the entity instead of `BuildTypeEntries` constructing a parallel `Entry`. `builder/Types/Entry.cs` (and `Field`/`EntryKind`) dissolves; `builder/Types/Render.cs` reads the entity. If the LLM's JSON shape must diverge from the entity's C# property shape, that's a thin render projection at the edge — coder's call — but the *knowledge* lives on the entity, never duplicated in a parallel struct.

**This is the part that reshapes behavior.** Treat the rendered LLM schema as the integration risk: capture the catalog the builder renders for a known set of types *before*, assert byte-identical *after*. If it can't be made deterministic, raise it before Stage 4 lands. (plang-types shipped `Tests/Types/` and the math/cut suites — check whether a golden already exists to extend rather than start fresh.)

## `data.Type` is the natural home, the registry holds the catalog

Every value is a `data`; a type is never free-floating — it is always something a value *has*. The entity's primary door is `data.Type`:

```csharp
// type/this.cs — the entity, moved out of app.data.type
// data/this.cs — Type navigates through the non-null context
public type Type => context.app.type[Value];   // Stage 2 made context non-null; the registry holds primitives too
```

`app.type[...]` is the registry door (loader, builder, conversion); `data.Type` is the door you hold. Both return `type.@this`. The CLR type lives *on* it: `data.Type.ClrType`.

## Migration: ~30 `App.Types.*` sites + 12 `Entry` sites

| Today | After |
|---|---|
| `app.Types.Get("int")` / `.Clr(name)` | `app.type[name]` (→ `.ClrType` for the `System.Type`) |
| `app.Types.GetTypeName(t)` / `.Name(t)` | `app.type[t].Name` (reverse selection by `System.Type`) |
| `app.Types.GetValidValues(t)` | `app.type[t].ValidValues` |
| `app.Types.IsClrTypeName(name)` | stays on the registry (it's a registry query, not a per-type fact) |
| `app.Types.Scheme.*` / `.Choices.*` / `.Kinds.*` / `.Renderers.*` | child registries, reached via `app.type.scheme` / `.choice` / `.kind` / `.renderer` |
| `builder.Types.Entry` / `Field` / `EntryKind` (12) | fold into `type.@this`; builder renders off the entity |
| `app.Types.BuildTypeEntries` / `ComplexSchemas` | stay on the collection `type.list.@this` (the catalog walk) |
| `data.Type.ClrType` (already exists) | unchanged — the entity just moves namespace |

`of<T>()` has no current caller — every site is a runtime string (`p.Type.Value`) or a reflected `System.Type` (`prop.PropertyType`). Provide it for ergonomics; the load-bearing operations are `[name]`, `[System.Type]`, and `.Name`/`.ClrType` on the entity.

## Cleanup that rides with the move

- **`data/Converter.cs` is dead.** It's a `[TypeConverter]` on the `type` class for a Newtonsoft path; Newtonsoft is fully gone (no package ref, no `using`). Delete the converter and the `[TypeConverter]` attribute. Live (de)serialization is `data/Json.cs` (System.Text.Json `JsonConverter<type>`), which moves with the entity.
- Files that reference the entity and update when it moves: `data/this.cs` (the class def + the `Type` property), `data/Json.cs`, `data/Wire.cs`, `data/Converter.cs` (deleted).

## Scope and sequencing

- Depends on the **non-null invariant** (Stage 2 — so `data.Type => context.app.type[Value]` has no fallback) and the **`app.type` accessor** (Stage 3).
- This is the one piece that genuinely *moves logic*. Keep the registry's selection/lifecycle/catalog-walk clean and put every per-type fact on the entity — same rule as everywhere else.
- If the builder schema reshape balloons, the fold (decision 2) is the natural cut point to split back out — land the move (1B) without the Entry fold, leave `Entry` parallel for a follow-up. Flag to Ingi before splitting.
