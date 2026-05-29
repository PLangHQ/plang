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
| `module/registry.cs` resolves an action with the registry off `app.@this` | C# | green |
| The four `App*` aliases no longer exist | C# (compile) | negative |
| `event` register/unregister/getbindings round-trip | C# | green |
| `format`/`error`/`navigator` selection works under new shape | C# | green |

### Nullability (stage 2) — `nullability.md`

| Behavior | Layer | Sense |
|---|---|---|
| `app.@this`/`context` are non-null where flipped (no `?.` remains on them) | C# (compile/review) | green |
| Un-stamped `data` read of `.Type` fails hard (no silent fallback) | C# + integration (cut 4) | negative |
| A stamped `data` resolves `.Type` for a primitive without a static fallback | C# | green |
| `app.Parent` remains nullable (root has no parent) | C# | green |

### Type entity (stage 4) — `type-entity.md`

| Behavior | Layer | Sense |
|---|---|---|
| `app.type["int"]` returns a `type.@this` with `.name`/`.clr` | C# | green |
| `app.type[runtimeType].name` gives the PLang name (reverse direction) | C# | green |
| `data.Type` returns the type entity via `context.app.type[Value]` | C# | green |
| `app.type[t].validValues` / scheme reachable on the entity | C# | green |
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

Impossible-by-design (no test): `app.@this` itself null (root always exists); `app.goal`/`app.type` collection node null (always a real, possibly-empty collection — only `.current` and `[miss]` can be null).

## 3. New surfaces this branch introduces

### Interfaces and types
- `goal.list.@this` (`goal/list/this.cs`) — goal collection: `this[string]`, `this[path]`, `Add`, `Remove`, `Contains`, `IReadOnlyList<goal> list`, `goal? current`.
- `channel.list.@this` (`channel/list/this.cs`) — channel collection: `this[string]`, `Add`, `Remove`, `Contains`, enumerate. No I/O.
- `type.list.@this` (`type/list/this.cs`) — type collection: `this[string]`, `this[System.Type]`, `of<T>()`, `Register`, enumerate.
- `event.list.@this`, `error.list.@this`, `format.list.@this`, `variable.list.@this`, `variable.navigator.list.@this`, `type.choice.list.@this` — registries moved from the old plural `this.cs`.
- `type.@this` (stage 4) — the promoted entity: `.name`, `.clr`, `.scheme`, `.validValues`, conversion.
- `module/registry.cs` — the action registry, no longer a `this.cs`, held on an internal field of `app.@this`.

### New methods / members on existing types
- `channel.@this.Write(data)` / `Read()` — abstract; `stream` overrides. (No `WriteText`.)
- `data.@this.Type` — returns `type.@this` (stage 4), was `ClrType : System.Type?`.
- `goal.@this.List` (and other elements) — navigation back to the collection via the App back-ref, if the coder keeps it.

### New registrations
- None (no new MIME types / channels). The miss-policy setting is new config — name + default chosen with test-designer.

### Existing surfaces this branch touches by reference (already real)
- `CallStack.Current.Action.Step.Goal` (AsyncLocal) — the source `app.goal.current` reads.
- `builder.Types.Entry` + `BuildTypeEntries`/`ComplexSchemas`/`Render` — reshaped in stage 4, golden-pinned.
- `app/GlobalUsings.cs`, `PLang.Tests/GlobalUsings.cs` — alias RHS retargeted (stage 1), four aliases deleted (stage 3).
- The ~286 registry call sites (Types 80, Variables 63, Channels 37, …) — migrated, regression-covered.
