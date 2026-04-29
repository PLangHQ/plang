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
