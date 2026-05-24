# CLAUDE.md / character-file change proposals — compile-llm-notes-per-action

**Status:** v1 and v2 both APPLIED in the same docs pass that filed them — proposals retained here for the record (per workflow). Verified against current source before applying: `PLang.Generators/Emission/Property/{Data,Code}/` (Code folder exists, no Provider folder); `[Code]` attribute live in `PLang/app/Attributes/`; PLNG001 diagnostic text says `[Code]` (`PLang.Generators/Discovery/this.cs`); zero class-level `[Description]`/`[ModuleDescription]`/`[Example]` usages remain on action handlers (one property-level `[Description]` survives on `goal.return.Depth` documenting the parameter; out of scope for this rename).


## docs — v1 — 2026-05-24
**Target:** `/workspace/plang/CLAUDE.md`
**Why:** Branch renamed the action handler attribute `[Provider]` → `[Code]` across source, source generator, PLNG001 text, and the emission-property folder (`Emission/Property/Provider/` → `Emission/Property/Code/`). CLAUDE.md still says `[Provider]` in two places (the Source Generator OBP-shape line and the property-kinds PLNG001 bullet), which will mis-train future agents reading this file. Mechanical rename — no semantics change.
**Proposed change:**

Replace in the "Source Generator" section:

```
- OBP shape: entry `PLang.Generators/this.cs` → `Discovery/this.cs` (Roslyn boundary) + `Emission/Action/this.cs` (per-handler) + `Emission/Property/{Data,Provider}/this.cs` (polymorphic per-property)
```

with:

```
- OBP shape: entry `PLang.Generators/this.cs` → `Discovery/this.cs` (Roslyn boundary) + `Emission/Action/this.cs` (per-handler) + `Emission/Property/{Data,Code}/this.cs` (polymorphic per-property)
```

Replace the leading clause of the "Property kinds (PLNG001 build-time gate)" bullet:

```
- **Property kinds (PLNG001 build-time gate)**: action handler properties must be `Data<T>` (or plain `Data`) or `[Provider] T`. Anything else fails the build with `PLNG001`. …
```

with:

```
- **Property kinds (PLNG001 build-time gate)**: action handler properties must be `Data<T>` (or plain `Data`) or `[Code] T`. Anything else fails the build with `PLNG001`. …
```

And in the "Key Files" section:

```
- PLang.Generators/this.cs — source generator entry point (`Discovery/`, `Emission/Action/`, `Emission/Property/{Data,Provider}/` underneath)
```

→

```
- PLang.Generators/this.cs — source generator entry point (`Discovery/`, `Emission/Action/`, `Emission/Property/{Data,Code}/` underneath)
```

## docs — v2 — 2026-05-24
**Target:** `/workspace/plang/CLAUDE.md`
**Why:** Branch moved per-action LLM teaching (`Description`, `ModuleDescription`, `Example`) from C# attributes to markdown files under `os/system/modules/<module>/`. This is the kind of architectural rule new agents need before they think "let me add `[Example]` to my new action" — needs one bullet in Runtime2 Conventions so it surfaces in the file every agent reads first. The full rule + adding-an-action walkthrough is in `Documentation/v0.2/action-catalog.md`, and the deeper "why" is in `good_to_know.md` ("Per-action LLM teaching lives in markdown, not attributes").
**Proposed change:**

Append a new bullet under "## Runtime2 Conventions" (placement: near the existing `[Code]`/property-kinds bullet so the two related rules sit together):

```
- **Action prose lives in markdown, not attributes.** Class shape (parameters, types, modifier role, defaults) goes in C# attributes on the action handler. **Prose** (Description, Notes, Examples) goes in `os/system/modules/<module>/{module,<action>}.{description,notes,examples}.md`. `[Description]`/`[ModuleDescription]`/`[Example]` no longer exist on action handlers — don't add them back. Per-action Notes render in the user message of each Compile call **only when the planner picked that action**; `Compile.llm` keeps only the cross-cutting kernel. `module.*.md` is a reserved stem (module-wide teaching layer); the renderer concats module-first + blank line + action. Orphan files surface as warnings via `MarkdownTeaching.ScanOrphans`. Full guide: `Documentation/v0.2/action-catalog.md`; loader: `PLang/app/modules/MarkdownTeaching.cs`.
```
