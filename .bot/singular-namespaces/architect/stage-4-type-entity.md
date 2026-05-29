# Stage 4: Promote `type.@this` to an entity

> Code/paths here are suggestions that pin the shape — you own the final form. See "You own this" in `plan.md`.

**Goal:** Make `type.@this` PLang's version of `System.Type` — one entity representing a PLang type, owning its `Name`, `ClrType`, `Scheme`, `ValidValues`, and conversion — consolidating today's scattered `System.Type` + `builder.Types.Entry` + scheme/name strings. `app.type[...]` / `app.type.of<T>()` return the entity; `data.Type` returns it, and `data.Type.ClrType` returns the `System.Type`.

**Scope.** Included: the `type.@this` entity shape, `type.list.@this` as its registry (selection + lifecycle), `data.Type` returning the entity, `data.ClrType` relocating to `data.Type.ClrType`, and reshaping the builder schema path (`BuildTypeEntries`, `ComplexSchemas`, `builder/type/Render.cs`) to read off the entity instead of constructing a parallel `Entry`. Excluded: nothing downstream — last stage. If the builder schema reshape balloons, this is the natural cut point to split back out (flag to Ingi).

**Deliverables:** `type.@this` owns `Name`/`ClrType`/`Scheme`/`ValidValues` + conversion; `type.list.@this` is selection + lifecycle only; `data.Type => context.app.type[Value]` (the clean one-line form); `data.ClrType` → `data.Type.ClrType` at every consumer; the ~31 type-knowledge call sites reading off the entity; the builder schema path rendering from the entity; the internal recursive `GetTypeNameStatic` folded into the entity (or retired); clean rebuild + both suites green, with the builder golden output unchanged.

**Dependencies:** Stage 2 (non-null `context` — so `data.Type` has no fallback) and Stage 3 (the `app.type` accessor exists).

## Design

Full reasoning in `plan/type-entity.md`. The frame (Ingi's): `type.@this` is PLang's `System.Type` — a real object you work with — and the CLR type is a property *of* it (`data.Type.ClrType`), not a flat field on `data`. Today "a type" is smeared across `System.Type`, `builder.Types.Entry`, and name/scheme strings; this consolidates it. The registry `type.list.@this` does only selection (`[name]`, `[Type]`, `of<T>()`) and lifecycle (`Register`, `RegisterDomainTypes`); every per-type fact lives on the entity.

The migration's load-bearing replacements:
- `data.ClrType` (flat `System.Type?`) → `data.Type.ClrType` (navigate through the entity).
- `app.Types.Get(name)`/`.Clr(name)` → `app.type[name]` (→ `.ClrType` for the `System.Type`).
- `app.Types.GetTypeName(runtimeType)`/`.Name(t)` → `app.type[t].Name` (the reverse direction that genuinely needs the entity — an indexer returning bare `System.Type` couldn't carry `.Name`).

The **integration risk** is the builder schema path. `builder.Types.Entry` is what the LLM sees; `BuildTypeEntries`/`ComplexSchemas`/`Render` construct it today. After promotion they read from `type.@this`. The behavior that must not drift is the *rendered schema the builder sends to the LLM* — pin it with a golden test (capture before, assert byte-identical after; see the integration cut in `plan/test-coverage.md`).

`of<T>()` (compile-time generic selection) has **no current caller** — every real site is a runtime string (`p.Type.Value` from a `.pr`) or a reflected `System.Type` (`prop.PropertyType`). Provide it for ergonomics but don't over-invest; the load-bearing operations are `[name]`, `[Type]`, and `.Name`/`.ClrType` on the entity.

This is the one stage that moves logic rather than shape. Keep the registry/element rule intact: selection + lifecycle on `type.list.@this`, all knowledge on `type.@this`.
