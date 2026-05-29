# Stage 1: Type identity — registry fold, `kind` field, `Build()`, typed-property catalog

**Goal:** Make "high-level `type` + optional `kind`" the build-time identity of every value — folded into the existing `[PlangType]` registry, with each type deriving its own `kind` via a `Build(value)` method, and the LLM catalog rendering typed properties so it can navigate (`image(path) => …, Path(path)`).
**Scope.** Build-time type identity only. *Included:* fold the flat `Primitives` table into the registry; the `kind` field on `.pr` parameters; the per-type `static Build(value)→kind` hook + its discovery; `Modules.Describe`/catalog emitting `type` + `kind` + typed properties. *Excluded:* serialization dispatch (Stage 2), any concrete new type (Stages 3–6), runtime loading (Stage 7). No `number`/`image`/`code` yet — this stage stands up the machinery and proves it on existing types.
**Deliverables:**
- `app/types/this.cs` — the flat `Primitives`/`PrimitiveNames` dictionaries (`:34`, `:79`) and `IsPrimitive` (`:430`) route through the `[PlangType]` registry instead of a parallel dict. CLR primitives keep entries via a bootstrap registration; no behavior regresses.
- A `kind` field on the `.pr` parameter shape (`{name, value, type, kind?}`), omitted when absent. Wherever the builder writes a parameter's `type`, it now also writes `kind` when the type produces one.
- A discovered per-type `static string? Build(object? value)` convention (the **type** `Build` — distinct from the action `IClass.Build()`). The builder calls it when stamping a literal/typed value to fill `kind`. Discovery mirrors how `Resolve` is found (reflection on the static method).
- `Modules.Describe` / the type catalog emits, per type: the high-level name, what it resolves from (`image(path)`), its properties **with their types** (`Path(path)`), and — only for types that opt in (number) — its kind vocabulary.
- `app.formats` becomes the extension→PLang-name helper the parse-in side uses; it stops being a parallel universe (no behavior change required this stage beyond the registry pointing at it).
**Dependencies:** None (foundational). `Registry.cs` already exists with `[PlangType]` discovery, `ResolveName`, `ResolveType` (runtime-first), `RegisterRuntime`.

## Design

> **You own the code.** Snippets and file lists here are the intended shape, not literal dictation — match the surrounding codebase and use your judgment on the final form.

The registry is already 80% of this stage — `Registry.cs` discovers `[PlangType]`, resolves names both ways, and favors runtime registrations. What's missing is (a) retiring the *second* source of truth (the flat `Primitives` dict in `this.cs:34`) so there's one registry, and (b) the `kind` half of the identity.

**Registry fold.** Today `app.types.@this.Get(name)` / `IsPrimitive` consult the flat `Primitives`/`PrimitiveNames` dicts. Route them through the registry. CLR primitives (`string`, `int`, `bool`, …) that have no folder still need name↔type entries — seed them with a small bootstrap `RegisterRuntime`-style registration at registry construction (they're not full PLang types: no folder, no `Resolve`, no `Build`). The acceptance bar is *no regression* — every existing `Get`/`IsPrimitive`/`Conversion` path resolves the same as before.

**The `kind` field.** A `.pr` parameter grows an optional `kind` alongside `type`. **Separate field, never a `type:kind` string** — splitting a string is runtime work and the whole point is the runtime does none. Omit `kind` when the type has none (a plain string, a `%var%` ref, a polymorphic `math.add` result whose kind is decided at runtime).

**The type `Build(value)` hook.** Each type that has a kind exposes `static string? Build(object? value)` — the build-time sibling of `Resolve`. It reads the value's refinement *without constructing the value*: `number.Build(3.5)→"decimal"`, `path.Build("https://…")→"http"`. The builder calls it when it stamps a typed literal. Discovery is by reflection on the static method, same as `Resolve`.

- **Naming:** this is the *type's* `Build`, distinct from the *action handler's* `IClass.Build()` (which decides an action's return type — `file.read.Build()`). They cooperate (see [plan/build-vs-runtime.md](plan/build-vs-runtime.md) "Two Builds"). If the collision proves confusing in code, rename the type method to `KindOf(value)` — your call; the plan uses `Build` for the build/resolve symmetry.

**Typed-property catalog.** This is the most intricate part — give it attention. The catalog the LLM reads must let it navigate member access: `%photo.Path.Exists%` only type-checks if the catalog says `image` has a `Path` property of type `path`, and `path` has `Exists`. So `Modules.Describe` (or whatever renders the type catalog into the compile prompt) emits, per type, its properties annotated with their types — `image(path) => Exif, Width, Height, Path(path)`. The `(path)` after `image` is what the type *resolves from*; the `(path)` after `Path` is that property's type. See [plan/types.md](plan/types.md) "The pattern, restated" and [plan/build-vs-runtime.md](plan/build-vs-runtime.md) "composition, not union".

**What proves it this stage:** with no new type added, an existing CLR-backed value (e.g. a literal `int`) round-trips through the registry, and the catalog renders a type's properties with their types. The `kind` field is exercised once a type with `Build` exists — `path` is the natural first (`path.Build` → scheme), which it gets in Stage 2 as the dispatch first-mover; if you want a kind exercised *here*, add `path.Build` in this stage and stamp `kind` for a literal path.

Risk to watch: the catalog format change touches the compile prompt the LLM consumes — verify a build still produces well-formed `.pr` output and the scope snapshot reads sensibly before moving on.
