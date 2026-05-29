# Stage 4: Promote `type.@this` to an entity

**Goal:** Make `type.@this` a real entity representing one PLang type — owning its name, CLR type, scheme, valid-values, and conversion — consolidating today's scattered `System.Type` + `builder.Types.Entry` + scheme/name strings. `app.type[...]` / `app.type.of<T>()` return the entity; `data.Type` returns it through `context.app.type[Value]`.

**Scope.** Included: the `type.@this` entity shape, `type.list.@this` as its registry (selection + lifecycle), `data.Type` returning the entity, and reshaping the builder schema path (`BuildTypeEntries`, `ComplexSchemas`, `builder/type/Render.cs`) to read off the entity instead of constructing a parallel `Entry`. Excluded: nothing downstream — this is the last stage. If the builder schema reshape balloons, this is the natural cut point to split back out (flag to Ingi).

**Deliverables:** `type.@this` owns per-type knowledge; `type.list.@this` is selection + lifecycle only; `data.Type => context.app.type[Value]` (the clean one-line form, now that the entity exists); the ~31 type-knowledge call sites (`Get`/`Clr`/`GetTypeName`/`Name`/`GetValidValues`/`IsClrTypeName`) reading off the entity; the builder schema path rendering from the entity; the internal recursive `GetTypeNameStatic` folded into the entity (or retired); clean rebuild + both suites green, with the builder golden output unchanged.

**Dependencies:** Stage 2 (non-null `context` — so `data.Type` has no fallback) and Stage 3 (the `app.type` accessor exists).

## Design

Full reasoning in `plan/type-entity.md`. The core move: "a type" stops being smeared across `System.Type` (the CLR type), `builder.Types.Entry` (the rich description), and name/scheme strings, and becomes one `type.@this` that owns all of it. The registry `type.list.@this` does only selection (`[name]`, `[Type]`, `of<T>()`) and lifecycle (`Register`, `RegisterDomainTypes`); every per-type fact lives on the entity (`.name`, `.clr`, `.scheme`, `.validValues`, the conversion the type knows how to perform).

The **integration risk** is the builder schema path. `builder.Types.Entry` is what the LLM sees; `BuildTypeEntries`/`ComplexSchemas`/`Render` construct it today. After promotion they read from `type.@this`. The behavior that must not drift is the *rendered schema the builder sends to the LLM* — pin it with a golden test (see `plan/test-coverage.md`) so the reshape is provably output-equivalent.

`of<T>()` (compile-time generic selection) has **no current caller** — every real site is a runtime string (`p.Type.Value` from a `.pr`) or a reflected `System.Type` (`prop.PropertyType`). Provide it for ergonomics but don't over-invest; the load-bearing operations are `[name]`, `[Type]`, and `.name`/`.clr` on the entity. The reverse direction (`Type → name`, today `GetTypeName`) becomes `app.type[runtimeType].name` and is the one that genuinely needs the entity (an indexer returning a bare `System.Type` couldn't carry `.name`).

This is the one stage that moves logic rather than shape. Keep the registry/element rule intact: selection + lifecycle on `type.list.@this`, all knowledge on `type.@this`.
