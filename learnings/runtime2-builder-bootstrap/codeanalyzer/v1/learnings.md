# Learnings — runtime2-builder-bootstrap v1

Concrete, reusable insights from this review pass. Save what generalizes; skip session-specific noise.

## Codebase patterns confirmed

- **Bare `catch` is the pervasive anti-pattern in this codebase.** When the codebase has a clean filter (`catch when (ex is not (NullReference | OutOfMemory | StackOverflow))`) and only some sites use it, scan exhaustively — drift is guaranteed. This branch added 5+ new bare-catch sites across `TypeConverter`, `Variables/this`, `FluidProvider`, `Errors/Error`, and the source generator's emitted code. Don't accept "it's just diagnostic" as justification.

- **`Try*` methods that return tuples must NEVER throw**, even for "this should never happen" cases. `TypeConverter.TryConvertTo` violates this with two `throw new InvalidOperationException` sites. The defensive intent is right; the implementation must be `return (null, Errors.Error)` — anything else bypasses the framework's error handling and surfaces stack traces to users.

- **Diagnostic probes left after a bug-hunt are dead code.** Search `_ = .*Debug.Write` and `[DIAG]` markers; if the original bug is fixed (commit messages will say "no longer recurring" / "guard didn't fire"), the probe goes. The `DefaultBuilderProvider.DiagGoal` walker is the canonical example here.

## OBP application notes

- **Static utility classes are acceptable when the operation is genuinely cross-type.** `TypeConverter.ConvertTo(any → any)` has no natural owner. `PlangTypeIndex` is a registry with the same justification. Don't manufacture an owner just to satisfy OBP rule 1; do flag the static-helper status so the next reader doesn't expect navigation.

- **Renderer/format static classes are an OBP smell but often the pragmatic call.** `ExampleRenderer.Render(spec, modules)` could be `spec.Render(modules)` — same code, better OBP. If the spec is a pure data record meant to be JSON-friendly, leave the static; flag the trade-off so the team chooses consciously.

- **Three-or-more places implementing the same algorithm = drift hazard.** The branch has three "formal syntax" renderers (ExampleRenderer + FluidProvider + DefaultBuilderProvider). When the comment in one says "Mirrors X for the Y path", that's the smell — the second comment is admitting the duplication. Extract immediately, before the next syntax change.

## Behavioral-reasoning techniques that paid off this round

- **Trace data origins for any cast.** `TypeConverter`'s `IConvertible` branch (line 105 of ExampleRenderer / 138 of FluidProvider) calls `value.ToString()` — that's culture-sensitive. Spotting it required asking "what types flow through here?" not "is the cast safe?".

- **Clone/Copy family audit when properties are added.** `Actor.Context.@this` got new `Trace` and `Error` properties; `Step` got `PriorText, Guidance, Level, Confidence, Formal, Source, Keep`; `Action` got `Description, ModuleDescription, IsModifier`; `Error` got `Params, Details`. EVERY copy method in those classes needs verification. The asymmetric Clone vs CreateChild in Context is the visible tip.

- **Deletion test on every fix-introduced block.** Branches like this layer fix on fix; each fix often adds defensive code that survives past its purpose. Probe blocks, validation re-runs, "safety net" double-validations — ask "what test breaks if this goes?"

## Codebase-specific knowledge gained

- **`Data.@this.IsDeferredActionTemplate`** (PLang/App/Data/this.cs:519) does string-name detection on PLang type names `"action"`/`"list<action>"`. Live key-name heuristic — fragile to renames or user [PlangType("action")] aliases.

- **`PlangTypeIndex.IsClrTypeName`** is the system-wide guard against the historic Fluid-template `ToString()` leak that wrote `App.Goals.Goal.GoalCall` into a goal-name slot. The guard itself is sound; the way it's invoked (via throws from `TryConvertTo`) is wrong.

- **`Modules.Describe()` caches** module-level `[ModuleDescription]` per namespace per call. Called once at builder startup — not per step.

- **The source generator (`LazyParamsGenerator`) emits per-handler `__SnapshotParams` and `__action`/`__step`/`__callFrames` plumbing**. When reviewing handlers, check the generated code too — it's where the bare-catch in `ExecuteAsync` lives. The generated catch shape should match hand-written code's filter shape.

- **Builder-mode `_context.App.Building.IsEnabled`** short-circuits Variables.Resolve on typed objects (Variables/this.cs:478). Three levels of `?.` indirection — fragile to refactors. Worth a `Context.IsBuilding` shortcut.

## Process insights

- **When the coder's report describes a tiny scope but the actual diff is huge**, the work has expanded. Re-scope the review explicitly in the plan before approval; otherwise the session balloons. This branch's coder report named 3 gap fixes; the actual diff was 2347 files.

- **Squashed mega-commits hide the order of fixes.** The branch had `50351d8b` squashing 441 commits. Recent post-squash commits (`ada1901a`, `711c2107`) tell the story of a leak hunt that ended in success — and the diagnostic probe left behind. Read the recent commit messages before deep-diving into files; they map the WHAT to the WHY.

- **Tier reviews scale.** ~14 files deep + ~12 light = ~3 hours of analyst time, ~2k lines of careful reading, 10 actionable findings. Don't try to deep-dive 50+ files in one pass.

## What to flag in future reviews

- **Bare `catch` in any wrapper or `Try*` method** — automatic finding.
- **Throw from a method named `Try*` or returning a `(value, error)` tuple** — automatic finding.
- **Diagnostic probe code (`Debug.Write` walks, `if (param.Name == "...")` heuristics)** — check if the bug it hunts is still a thing.
- **Property added to a class with multiple copy methods** — audit Clone, CreateChild, ctor, factory, deserialization.
- **String-name detection on type names, parameter names, or kind discriminators** — flag for structural replacement.
- **Three+ places implementing the same format/algorithm** — extract.
- **Culture-sensitive `ToString()` on numbers, dates, bools in formatters** — use `InvariantCulture`.
