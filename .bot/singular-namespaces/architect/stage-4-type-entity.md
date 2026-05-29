# Stage 4: Land the type entity in `type/` and fold Entry

> Code/paths here are suggestions that pin the shape — you own the final form. See "You own this" in `plan.md`.

**Goal:** plang-types already made `data.Type` return a type entity (`app.data.type`, with `.ClrType`/`.Kind`/`.Convert`) and removed the flat `data.ClrType`. This stage gives that entity its right home and right ownership: **move** it from `app.data` to `type/this.cs` (`type.@this`), **demote** the big registry to `type/list/this.cs` (`type.list.@this`), and **fold** `builder.Types.Entry` onto the entity so the builder renders off it instead of constructing a parallel struct. `data.Type` and `app.type[name]` both return `type.@this`; `data.Type.ClrType` gives the `System.Type`.

**Scope.** Included: relocating the `type` class out of `app/data/this.cs` into `type/this.cs`; updating the entity's references (`data/this.cs`, `data/Json.cs`, `data/Wire.cs`); deleting the dead `data/Converter.cs` + its `[TypeConverter]` attribute (Newtonsoft is gone); folding `builder.Types.Entry`/`Field`/`EntryKind` (12 sites) onto `type.@this`; reshaping the builder schema path (`builder/type/Render.cs`, and `BuildTypeEntries`/`ComplexSchemas` which stay on `type.list.@this` as the catalog walk) to read from the entity; `data.Type => context.app.type[Value]` in its clean non-null form. Excluded: nothing downstream — last stage. **If the Entry fold balloons, that's the cut point** — land the move (1B) and leave `Entry` parallel for a follow-up; flag to Ingi.

**Deliverables:** `type.@this` at `type/this.cs` owning `Value`/`Name`/`ClrType`/`Kind`/`Scheme`/`ValidValues`/`Convert` **plus** the folded Entry knowledge (classification record/enum/scalar, `Fields`, `Shape`, `ConstructorSignature`, `Properties`, `Example`, `Description`, `Kinds`); `type.list.@this` as selection + lifecycle + DLL-load + catalog-walk only; `app.type[name]`/`[System.Type]`/`of<T>()` return the entity; `data.Type => context.app.type[Value]`; `data.Converter.cs` deleted; `builder.Types.Entry` dissolved, builder rendering from the entity; the ~30 `App.Types.*` and 12 `Entry` sites reading off the entity; clean rebuild + both suites green **with the builder golden output byte-identical**.

**Dependencies:** Stage 2 (non-null `context` — so `data.Type` has no fallback) and Stage 3 (the `app.type` accessor + entity-returning indexer exist; the registry is already `type.list.@this` from Stage 1).

## Design

Full reasoning in `plan/type-entity.md`. The frame (Ingi's): `type.@this` is PLang's `System.Type` — a real object representing one PLang type — and everything about a type is *on it*. plang-types proved the value-side half (the entity, `data.Type.ClrType`). This stage completes it: the entity lives in the `type` namespace, the registry is the collection beneath it, and `builder.Types.Entry` — which is just "the type, described for the LLM" — folds in.

**The move.** The `type` class is currently defined inside `app/data/this.cs` (lines ~18–76). Relocate to `type/this.cs` as `type.@this`. `data.Type` keeps returning it; `data/Json.cs` (the System.Text.Json converter) and `data/Wire.cs` (deserialization) move their `using`/refs to the new home. `data/Converter.cs` is dead (Newtonsoft removed) — delete it and the `[TypeConverter]` attribute on the class.

**The demote.** `type.list.@this` (already at `type/list/this.cs` from Stage 1) keeps only selection (`[name]`, `[System.Type]`, `of<T>()`), lifecycle (`Register`, `Loader`/DLL load), and the catalog walk (`BuildTypeEntries`/`ComplexSchemas` — enumerate-and-project; the transitive discovery is a collection operation). Per-type facts move onto `type.@this`. The four child registries (`Choices`, `Scheme`, `Kinds`, `Renderers`) stay as children, reached via `app.type.*`.

**The fold (the behavior-moving part).** `builder.Types.Entry` is the PLang type described for the LLM — every field is intrinsic to the type (table in `type-entity.md`). The entity absorbs them (computed lazily by reflecting over its `ClrType`); `builder/type/Render.cs` renders from the entity; `Entry.cs`/`Field`/`EntryKind` dissolve. If the LLM's JSON shape must diverge from the entity's C# shape, that's a thin render projection at the edge — but the knowledge lives on the entity, never in a parallel struct.

**The integration risk is the rendered LLM schema.** Pin it with a golden test: capture the catalog the builder renders for a known set of types *before* the fold, assert byte-identical *after*. plang-types shipped `Tests/Types/` and the math/cut suites — check whether a golden already exists to extend before writing a fresh one. If the schema can't be made deterministic, raise it before this stage lands.

`of<T>()` has no current caller — every site is a runtime string or a reflected `System.Type`. Provide it for ergonomics; the load-bearing operations are `[name]`, `[System.Type]`, and `.Name`/`.ClrType` on the entity.

This is the one stage that moves logic rather than shape. Keep the registry/element rule intact: selection + lifecycle + catalog-walk on `type.list.@this`, every per-type fact on `type.@this`.
