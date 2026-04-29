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
