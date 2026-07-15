# Plan: relocate action modules to `app.module.action.*`

**Status: awaiting go-ahead. No code yet.** Emerged during 4a: `app.module.list` is the module-concept's own `.list` face AND the `list` action module — collision. Fix the root, not the symptom: action-handler code should not live inside the module concept's namespace.

## Goal
Free `app.module.*` for the module CONCEPT:
- collection `app.module.list.@this`, element `app.module.@this`, selection `app.module["x"]`.
Move ALL action-handler code to `app.module.action.<name>` (Ingi: all modules, incl. the merged engine-concepts — "they're actions too, they just touch app").

## Scope (measured)
- **30 module folders / 155 files**: `app/module/<name>/` → `app/module/action/<name>/`, namespace `app.module.<name>` → `app.module.action.<name>`.
- Includes the 7 merged engine-concepts: `cache`, `code`, `build`, `debug`, `callback`, `setting`, `module` — move wholesale (their infra types too: `ICache`, `Debug`, `Builder`, `AppCode`, …).
- **Stays at `app.module`** (module-concept infra, not actions): `list/this.cs` (the collection), `Attributes.cs`, `ICodeGenerated.cs`, `IClass.cs`, `MarkdownTeaching.cs`, and the coming element `this.cs`.

## The one load-bearing knob
`list/this.cs Discover()` derives the module name from the namespace segment after `baseNamespace` (`type.Namespace[(baseNamespace.Length+1)..]`). Default `baseNamespace = "app.module"` → change to **`"app.module.action"`**, else every module is named `action.file`, `action.list`, …. Two sites: the `??=` default and the ctor's `Discover(assembly, "app.module")`. (External DLLs via `module.add` also pass a namespace — unchanged, they name their own root.)

## Steps
0. **(architect)** bless the model; update the CLAUDE.md convention (the "seven merged concepts under `app/module/<name>`" bullet, and the `app.X` home for those concepts) + the Stage 4 plan. This changes a documented convention — architect/docs own that.
1. `git mv app/module/<name>/ → app/module/action/<name>/` for the 30 action folders (concept-infra files stay).
2. Rewrite `namespace app.module.<name>` → `app.module.action.<name>` in moved files.
3. Flip discovery `baseNamespace` → `"app.module.action"` (both sites).
4. Update `GlobalUsings` aliases (`AppCode`, `ICache`, `Debug`, …) + every `using app.module.<x>` / `global::app.module.<x>` — compiler-guided build-fix loop (the compiler enumerates every site, as in the 4a relocation).
5. Build green; baseline diff by name (no new reds).
6. **Resume 4a**: the collection now sits unambiguously at `app.module.list.@this`; add the element at `app.module.@this`; fold choice registration; delete `RegisterModuleChoiceTypes`/`GetChannelInventory`.

## Risks
- **Large diff** (~155 file moves + hundreds of ref updates). Mechanical; compiler-guided. Highest churn risk if interrupted midway — do it as its own commit(s), green before 4a resumes.
- **Discovery is the single point of failure**: if `baseNamespace` isn't flipped, `Discover` registers zero (or mis-named) actions and the whole runtime finds no actions. Verify with a smoke build + one action dispatch before declaring green.
- **Merged-concept infra under `.action.`**: `ICache`/`Debug`/`Builder`/`AppCode` now live at `app.module.action.*` — a documented-convention change (why architect should re-bless).
- Deep action namespaces (e.g. a module's `code/` subfolder) keep their relative depth (`app.module.action.<name>.<sub>`) — module name derivation is unaffected.

## Sequencing vs the in-flight 4a
My 4a relocation (`module/this.cs` → `module/list/this.cs` = `app.module.list.@this`) is **correct and kept** — it just needs the list ACTIONS to vacate to `app.module.action.list` for the ambiguity to clear. So: this namespace move lands FIRST (or bundled), then 4a resumes clean. Current 4a edits stay uncommitted until we decide order.

## Architect involvement — recommend YES
This wasn't in the architect's Stage 4 plan (it assumed `app.module.list` was free) and it changes a foundational convention + the module/action concept model. Architect should own the model coherence, the CLAUDE.md convention update, and the plan revision; I own the mechanical execution. A short architect pass to bless `app.module.action.*` + reconcile it with `module`/`action`-as-hosts avoids a convention drift the docs bot would later have to untangle.
