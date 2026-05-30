# Test coverage (reference)

One row per behavior the plan commits to. Test-designer writes one test per row.

## 1. Coverage matrix

### Rename (stage 1) — `rename-map.md`

| Behavior | Layer | Sense |
|---|---|---|
| A goal builds and runs end to end after rename (generator strings correct) | integration (cut 1) | green |
| Generated action code compiles and dispatches (`module.action` resolves) | integration (cut 1) | green |
| Both existing suites green after each subsystem rename | regression | green |
| `app.<x>` namespaces resolve under new names (no dangling `app.<old>`) | C# (compile) | green |

### Accessor (stage 3) — `accessor-model.md`

| Behavior | Layer | Sense |
|---|---|---|
| `app.goal["Start"]` selects the named goal | C# | green |
| `app.goal[prPath]` selects by pr-path | C# | green |
| `app.goal.list` enumerates loaded goals (setup excluded) | C# | green |
| `app.goal.current` returns the executing goal mid-run | C# | green |
| `app.goal.current` is null at rest (nothing executing) | C# | negative |
| `app.type["int"]` selects by name | C# | green |
| `app.type.of<int>()` selects by CLR type | C# | green |
| `app.channel["output"]` selects the channel | C# | green |
| `actor.channel["output"].Write(data)` writes through the element | C# + integration (cut 2) | green |
| Channel registry exposes no I/O method (selection+lifecycle only) | C# | green |
| `app.module["file"]` selects the file module; `.list` enumerates | C# | green |
| `app.module` resolves + dispatches an action (registry on the public surface, no demote) | C# + integration (cut 1) | green |
| `app.module` has no `.current` | C# (compile) | negative |
| The four `App*` aliases no longer exist | C# (compile) | negative |
| `event` register/unregister/getbindings round-trip | C# | green |
| `format`/`error`/`navigator` selection works under new shape | C# | green |

### Nullability (stage 2) — `nullability.md`

| Behavior | Layer | Sense |
|---|---|---|
| `app.@this`/`context` are non-null where flipped (no `?.` remains on them) | C# (compile/review) | green |
| Un-stamped `data` read of `.Type` fails hard (no silent fallback) | C# + integration (cut 4) | negative |
| A stamped `data` resolves `.Type` for a primitive without a static fallback | C# | green |
| The 5 structural back-refs (step→Goal, channel→Actor/Channels, …) are non-null | C# (review) | green |
| `app.Parent` remains nullable (root has no parent) | C# | green |
| `ctx`→`context` rename leaves suites green (no behavior change) | regression | green |

### Type entity (stage 4) — `type-entity.md`

Note: plang-types already shipped the entity (`data.Type`, `data.Type.ClrType`), so the first two rows are **regression pins** — keep them green while the entity moves home and absorbs `Entry`.

| Behavior | Layer | Sense |
|---|---|---|
| `data.Type.ClrType` returns the `System.Type` (already real; survives the move) | C# | green (regression) |
| `data.Type` returns the entity via `context.app.type[Value]` (already real) | C# | green (regression) |
| `app.type["int"]` returns the entity (`type.@this`) with `.Name`/`.ClrType` | C# | green |
| `app.type[runtimeType].Name` gives the PLang name (reverse direction) | C# | green |
| `app.type[t].ValidValues` / `.Scheme` reachable on the entity | C# | green |
| `app.type[t].Fields`/`.Shape`/`.Example` (folded from `Entry`) read off the entity | C# | green |
| Builder LLM schema byte-identical before/after the `Entry` fold | integration (cut 3) | green |

## 2. Failure matrix

| Failure mode | Detected by | Error type | Layer |
|---|---|---|---|
| `app.channel["nope"]` (unknown channel) | index-miss on selection | **throws** a typed error (uniform, no setting) | C# + integration (cut 2) |
| `app.goal["nope"]` (unknown goal) | index-miss on selection | **throws** a typed error | C# |
| `app.type["NotAType"]` (unknown type name) | index-miss on selection | **throws** a typed error | C# |
| Read `data.Type` before context stamped | non-null `Context` (no guard) | hard throw (NRE→fix at producer, or typed) | C# + integration (cut 4) |
| Generator emits an old namespace literal | clean rebuild → consumer compile | compile error / `Action not found` at run | integration (cut 1) |
| Writing to a read-only / wrong-direction channel | `channel.@this` `CanWrite` on the element | typed `data.Fail` | C# |

Impossible-by-design (no test): `app.@this` itself null (root always exists); `app.goal`/`app.type`/`app.module` collection node null (always a real, possibly-empty collection — only `.current` can be null; an index-miss throws, it doesn't return null).

## 3. New surfaces this branch introduces

### Interfaces and types
- `goal.list.@this` (`goal/list/this.cs`) — goal collection: `this[string]`, `this[path]`, `Add`, `Remove`, `Contains`, `IReadOnlyList<goal> list`, `goal? current`.
- `channel.list.@this` (`channel/list/this.cs`) — channel collection: `this[string]`, `Add`, `Remove`, `Contains`, enumerate. No I/O.
- `type.list.@this` (`type/list/this.cs`) — type collection (demoted from `app.types.@this`): `this[string]`/`this[System.Type]`/`of<T>()` returning the entity, `Register`, DLL load, the catalog walk (`BuildTypeEntries`/`ComplexSchemas`), enumerate. Child registries `Choices`/`Scheme`/`Kinds`/`Renderers` hang off it.
- `event.list.@this`, `error.list.@this`, `format.list.@this`, `variable.list.@this`, `variable.navigator.list.@this`, `type.choice.list.@this` — registries moved from the old plural `this.cs`.
- `type.@this` (stage 4) — the type entity (PLang's `System.Type`). **Already exists** as `app.data.type` (plang-types); stage 4 moves it to `type/this.cs` and folds `Entry` onto it. Owns `Value`/`Name`/`ClrType`/`Kind`/`Scheme`/`ValidValues`/`Convert` + folded `Fields`/`Shape`/`ConstructorSignature`/`Properties`/`Example`/`Description`/`Kinds`.
- `module.@this` (`module/this.cs`) — the action registry, reached at `app.module`; selection (`[name]`) + the 6 ops (`GetCodeGenerated`, `Discover`, `Describe`, `Contains`, `Remove`) + enumerate. No `.current`. (No demote — stays on the public surface.)

### New methods / members on existing types
- `channel.@this.Write(data)` / `Read()` — abstract; `stream` overrides. (No `WriteText`.)
- `data.@this.Type` — **already returns the type entity** (plang-types); the flat `data.ClrType` is already gone (CLR type via `data.Type.ClrType`). Stage 4 only relocates the entity's namespace (`app.data.type` → `type.@this`).
- `goal.@this.List` (and other elements) — navigation back to the collection via the App back-ref, if the coder keeps it.

### New registrations
- None. Index-miss is a hard error (throws), not a configurable setting — no new config.

### Existing surfaces this branch touches by reference (already real)
- `CallStack.Current.Action.Step.Goal` (AsyncLocal) — the source `app.goal.current` reads.
- `builder.Types.Entry`/`Field`/`EntryKind` (12 sites) — **dissolved** in stage 4 (folded onto `type.@this`); `BuildTypeEntries`/`ComplexSchemas` stay on the collection as the catalog walk; `Render` reads the entity. Golden-pinned (cut 3).
- `app.data.type` + `data/Json.cs` + `data/Wire.cs` — the type entity, moved to `type.@this` in stage 4. `data/Converter.cs` (dead Newtonsoft `[TypeConverter]`) deleted.
- `app/GlobalUsings.cs`, `PLang.Tests/GlobalUsings.cs` — alias RHS retargeted (stage 1), four aliases deleted (stage 3).
- The registry call sites (type ~30 `App.Types.*` + 12 `Entry`, Variables 63, Channels 37, …) + the `ctx`→`context` rename (214 identifiers, 36 files) — migrated, regression-covered.
- The 5 structural back-refs flipped non-null (stage 2) — regression-covered; spot-test if a flip surfaced a stamping fix.
