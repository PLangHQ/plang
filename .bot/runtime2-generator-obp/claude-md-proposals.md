## test-designer — v1 — 2026-04-29
**Target:** /PLang.Tests/CLAUDE.md
**Why:** Discovered while moving legacy `App/Memory/` test folder to mirror source. The global type aliases `Data` and `Variables` in `PLang.Tests/GlobalUsings.cs` shadow any sibling namespace with the same name — creating `PLang.Tests.App.Data` namespace breaks 32 sibling test files (CS0118: 'Data' is a namespace but is used like a type). File-level `using Data = global::App.Data.@this;` doesn't help because it duplicates the global (CS1537) AND the namespace still wins at sibling scope. Future work that wants test folders mirroring `PLang/App/Data/` or `PLang/App/Variables/` will hit this — they need to know the workaround upfront.
**Proposed change:**
Add a section like:

```markdown
## Folder/namespace clash with global type aliases

`PLang.Tests/GlobalUsings.cs` declares heavily-used type aliases:

    global using Data = global::App.Data.@this;
    global using Variables = App.Variables.@this;

These aliases conflict with same-named test namespaces. Do NOT create `PLang.Tests.App.Data` or `PLang.Tests.App.Variables` namespaces — they shadow the type aliases for all sibling test files (CS0118: '...is a namespace but is used like a type'). File-level `using` aliases cannot override this (CS1537 duplicate against the global, and the namespace still wins).

Convention: when a test folder needs to mirror `PLang/App/Data/` or `PLang/App/Variables/`, use the `*Tests` suffix on the folder/namespace to avoid the clash:

    PLang.Tests/App/DataTests/      → namespace PLang.Tests.App.DataTests
    PLang.Tests/App/VariablesTests/ → namespace PLang.Tests.App.VariablesTests

The same applies to any future global alias whose name is also a directory under `PLang/App/` (e.g., `Channel`, `Step`, `Goal`).
```

## coder — v1 — 2026-04-29
**Target:** /PLang/App/CLAUDE.md
**Why:** v4 plan called for deleting `[VariableName]`, but the variable.set / list.* handlers need the variable's *name* (not its value) — a first-class concept distinct from value lookup. After As<T>(Context), the resulting Data carries the parameter property's Name (e.g., "list"), not the variable name (e.g., "products"). [VariableName] remains the cleanest expression of "I want the name, not the value." Phase 5 enabled the build-time gate (PLNG001) but kept [VariableName] as a recognized exemption alongside Data<T>/[Provider]. Future work could fold this into As<T> by preserving the variable name on full-match resolution, but it's a contract change that needs design.
**Proposed change:**
Add to "Module conventions" or similar:

```markdown
## Property kinds (v4)

Action handler properties must be one of:
- `Data<T>` (or plain `Data`) — the standard form. Resolution flows through `As<T>(Context)`.
- `[Provider]` — eagerly injected from `App.Providers`.
- `[VariableName] string` — the variable's *name* with `%` markers stripped. Used by handlers that work with variable identity rather than value (variable.set, list.*).

Anything else fails the build with PLNG001.
```

## coder — v7 — 2026-05-01
**Target:** /PLang/App/CLAUDE.md
**Why:** v6 left `[VariableName]` as a "future-branch task". Architect/v5 retired it via `Data<App.Variables.Variable>`. The previous v1 proposal (above) describes the now-obsolete three-rule contract; this entry replaces it with the post-v5 two-rule contract and documents the Variable shape.
**Proposed change:**

Replace the previous "Property kinds (v4)" / "Property kinds (PLNG001 build-time gate)" section with:

```markdown
## Property kinds (PLNG001 build-time gate)

Action handler properties must be one of:
- `Data<T>` (or plain `Data`) — the standard form. Resolution flows through `As<T>(Context)`.
- `[Provider] T` — eagerly injected from `App.Providers`.

Anything else fails the build with PLNG001.

For parameters that name a variable (write targets, read-by-name lookups: `variable.set`, `list.*`, `loop.foreach.ItemName/KeyName`), use `Data<App.Variables.Variable>`. Variable implements `IRawNameResolvable`, which tells `Data.As<T>` to skip its `%var%` substitution branch and dispatch to `Variable.Resolve(raw, ctx)` directly. Both `value="%x%"` and bare `value="x"` slot forms collapse to `Variable { Name = "x" }`. Use `Foo.Value` at use sites — Variable's implicit `string` operator fires at method-call boundaries (`Variables.Get(Foo.Value)`), and its `ToString() => Name` makes string interpolation read naturally.
```

## coder — v7 — 2026-05-01
**Target:** /Documentation/v0.2/good_to_know.md
**Why:** Variable is a new shape that handler authors will encounter when adding modules under `App/modules/list/`, `variable/`, or future name-slot domains. The implicit-conversion behavior is non-obvious in one specific case (`var foo = X.Value` infers Variable, not string) — surfacing it in good_to_know prevents the inference surprise.
**Proposed change:**

Add a new section titled `## App.Variables.Variable — the variable-name carrier`:

```markdown
## App.Variables.Variable — the variable-name carrier

`Variable` is a record (`Name`, `RawValue`, `WasPercentWrapped`) used as the wrapped type in `Data<Variable>` for action parameters that *name* a variable rather than carry its value. It implements `IRawNameResolvable`, a marker that tells `Data.As<T>` to skip its `%var%` substitution branch and call `Variable.Resolve(raw, ctx)` directly. This makes both `value="%x%"` and bare `value="x"` slot forms collapse to `Variable { Name = "x" }` — symmetric, and works even when the named variable doesn't yet exist (e.g., `set %x% = 5` creating x for the first time).

**Why it exists:** Before v5, `[VariableName] string` was a transitional carve-out for slots whose value the source generator strips `%` from rather than resolving. `Data<Variable>` is the typed form: same payload, but lives in the same OBP shape as every other handler property (`Data<T>`), and provenance attaches at the wrapper level (`Data<Variable>.Signature`) for future signing without a third API shape.

**Implicit string conversion gotcha:** `Variable` defines `static implicit operator string(Variable v) => v.Name`, so `string s = name.Value` works. But `var s = name.Value` infers `Variable`, not `string`. If you need a string typed local, write `string s = name.Value;` (or extract `.Name` explicitly).

**WasPercentWrapped:** Records whether the slot was `%x%` or bare `x` on the wire. Not load-bearing today — surfaces the LLM-emission shape for a future build-time validator that warns on bare-name slot values.
```

## coder — v7 — 2026-05-01
**Target:** /Documentation/Runtime2/todos.md
**Why:** v6's hand-off entry framed `[VariableName]` removal as a future-branch task with `VarRef<T>` proposed as the likely path. This branch landed it via Variable + IRawNameResolvable, so the todo should close with a pointer to the v5 plan and the actual approach taken.
**Proposed change:**

Mark the `[VariableName]` migration entry from `2026-04-30` (hand-off entry written by docs/v1) as resolved. If the entry has the form `- [ ] ...`, change to `- [x] ...` and add a one-line tail: `Resolved by architect/v5 → coder/v7 on runtime2-generator-obp: introduced App.Variables.Variable + IRawNameResolvable bypass instead of the speculative VarRef<T>.`
