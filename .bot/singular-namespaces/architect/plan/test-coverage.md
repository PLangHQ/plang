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

| Behavior | Layer | Sense |
|---|---|---|
| `app.type["int"]` returns a `type.@this` with `.Name`/`.ClrType` | C# | green |
| `data.Type.ClrType` returns the `System.Type` (relocated from flat `data.ClrType`) | C# | green |
| `app.type[runtimeType].Name` gives the PLang name (reverse direction) | C# | green |
| `data.Type` returns the type entity via `context.app.type[Value]` | C# | green |
| `app.type[t].ValidValues` / `.Scheme` reachable on the entity | C# | green |
| Builder LLM schema is byte-identical before/after promotion | integration (cut 3) | green |

## 2. Failure matrix

| Failure mode | Detected by | Error type | Layer |
|---|---|---|---|
| `app.channel["nope"]` (unknown channel) | the configured miss policy on selection | typed `data.Fail` (default policy = error) | C# + integration (cut 2) |
| `app.goal["nope"]` (unknown goal) | same miss policy | null or typed fail per policy | C# |
| `app.type["NotAType"]` (unknown type name) | type registry selection | null / typed fail | C# |
| Read `data.Type` before context stamped | non-null `Context` (no guard) | hard throw (NRE→fix at producer, or typed) | C# + integration (cut 4) |
| Generator emits an old namespace literal | clean rebuild → consumer compile | compile error / `Action not found` at run | integration (cut 1) |
| Writing to a read-only / wrong-direction channel | `channel.@this` `CanWrite` on the element | typed `data.Fail` | C# |

Impossible-by-design (no test): `app.@this` itself null (root always exists); `app.goal`/`app.type`/`app.module` collection node null (always a real, possibly-empty collection — only `.current` and `[miss]` can be null).

## 3. New surfaces this branch introduces

### Interfaces and types
- `goal.list.@this` (`goal/list/this.cs`) — goal collection: `this[string]`, `this[path]`, `Add`, `Remove`, `Contains`, `IReadOnlyList<goal> list`, `goal? current`.
- `channel.list.@this` (`channel/list/this.cs`) — channel collection: `this[string]`, `Add`, `Remove`, `Contains`, enumerate. No I/O.
- `type.list.@this` (`type/list/this.cs`) — type collection: `this[string]`, `this[System.Type]`, `of<T>()`, `Register`, enumerate.
- `event.list.@this`, `error.list.@this`, `format.list.@this`, `variable.list.@this`, `variable.navigator.list.@this`, `type.choice.list.@this` — registries moved from the old plural `this.cs`.
- `type.@this` (stage 4) — the promoted entity (PLang's `System.Type`): `Name`, `ClrType`, `Scheme`, `ValidValues`, conversion.
- `module.@this` (`module/this.cs`) — the action registry, reached at `app.module`; selection (`[name]`) + the 6 ops (`GetCodeGenerated`, `Discover`, `Describe`, `Contains`, `Remove`) + enumerate. No `.current`. (No demote — stays on the public surface.)

### New methods / members on existing types
- `channel.@this.Write(data)` / `Read()` — abstract; `stream` overrides. (No `WriteText`.)
- `data.@this.Type` — returns `type.@this` (stage 4), was `ClrType : System.Type?`. CLR type now via `data.Type.ClrType`.
- `goal.@this.List` (and other elements) — navigation back to the collection via the App back-ref, if the coder keeps it.

### New registrations
- None (no new MIME types / channels). The index-miss-policy setting is new config — name + default chosen with test-designer.

### Existing surfaces this branch touches by reference (already real)
- `CallStack.Current.Action.Step.Goal` (AsyncLocal) — the source `app.goal.current` reads.
- `builder.Types.Entry` + `BuildTypeEntries`/`ComplexSchemas`/`Render` — reshaped in stage 4, golden-pinned.
- `app/GlobalUsings.cs`, `PLang.Tests/GlobalUsings.cs` — alias RHS retargeted (stage 1), four aliases deleted (stage 3).
- The ~286 registry call sites (Types 80, Variables 63, Channels 37, …) + the `ctx`→`context` rename (214 identifiers, 36 files) — migrated, regression-covered.
- The 5 structural back-refs flipped non-null (stage 2) — regression-covered; spot-test if a flip surfaced a stamping fix.
