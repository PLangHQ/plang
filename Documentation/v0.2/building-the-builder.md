# Building the Builder

How to rebuild PLang's own builder (`os/system/builder/`) — the part written in PLang that compiles `.goal` files into `.pr`. This is the bootstrap case: PLang building PLang. Get the invocation wrong and the builder either won't run or will silently produce broken `.pr` files that look like LLM hallucinations.

The reliability notes at the bottom of this file describe ongoing work to make the LLM-driven compile more deterministic — read those if you're touching builder prompts or the validator. If you just need to rebuild, the recipe is right here.

## The cardinal rule

**Never hand-edit a builder `.pr` file after a self-rebuild has produced an error.**

If you self-rebuild the builder and notice the output is wrong — an action is mis-bound, a parameter is missing, a `goal.call` got a CLR type name, anything — **scream the error**. Surface it. Make it noisy. The fix belongs in:

- the LLM prompt (`os/system/builder/llm/*.llm`), or
- the validator (`PLang/app/modules/builder/code/Default.cs:Validate`), or
- the action catalog / type teaching (`os/system/actions/v2/summary.md`, `PLang/app/data/types.cs`).

**Never** in the `.pr` file. Editing the `.pr` after a bad rebuild masks the bug, lets the same LLM mistake reappear next rebuild, and corrupts the source of truth (the builder produces `.pr` — the `.pr` is downstream of the builder, not a place to compensate for it).

The one exception is the *initial bootstrap* when source `.goal` semantics change faster than the builder can re-derive them — that's documented in the pre-flight audit below and is a one-time hand-patch before the first successful rebuild, not a post-build cleanup.

## The recipe

```bash
cd os/
plang '--build={"files":["system/builder/Build.goal","system/builder/BuildGoal.goal","system/builder/BuildGoal/Start.goal","system/builder/BuildGoal/Plan.goal","system/builder/BuildGoal/Validate.goal","system/builder/BuildGoal/LlmFixer.goal","system/builder/BuildStep/Start.goal","system/builder/BuildStep/Validate.goal"]}' build
```

Three things matter and all are non-obvious:

### 1. `cwd = os/`

Not `os/system/`. Not `os/system/builder/`. Not the repo root. Only `os/`.

The builder's `.pr` files stamp paths like `/system/builder/.build/buildgoal.pr` — those resolve relative to cwd. Only `cd os/` makes those land on the actual files. Run from anywhere else and you get `File not found: /.build/buildgoal.pr` or the build emits short-form paths that break the next run.

`os/.build/app.pr` is the app-root marker (`name: "os"`). All builder pathing assumes that app root.

### 2. File order matters — outer to inner

Pass the explicit ordered list via `--build={"files":[...]}` whenever you rebuild the builder. The order is the call chain, entry first, leaves last:

1. `Build.goal` — builder entry
2. `BuildGoal/Start.goal` — per-goal orchestrator (owns `BuildSubGoal`, `HandleBuildFailure`)
3. `BuildGoal/Plan.goal` — single LLM call that returns the action sets per step (owns `QueryAndValidatePlan`)
4. `BuildGoal/Validate.goal` — structural validation after step compile
5. `BuildGoal/LlmFixer.goal` — re-prompt on validation failure
6. `BuildStep/Start.goal` — per-step compile (owns `Compile`, `QueryAndVerify`, `RefineActions`, `FixValidation`, `HandleStepFailure`, `EmitSummary`)
7. `BuildStep/Validate.goal` — per-step action validation (owns `ValidateAction`)

**Why:** during the rebuild the running app uses the *previous* in-memory build pipeline. If `BuildGoal`'s `.pr` is rewritten before its dependencies are stable, subsequent goal builds may pick up a partially-updated pipeline and produce inconsistent output. The list order is honoured by `DefaultBuilderProvider.LoadFiles` (`PLang/app/modules/builder/code/Default.cs`) — files in the `files` filter are queued in the order they appear.

**Wrong-order symptom:** every goal logs `Validation failed: StepResults or Goal is null — retrying...` on first attempt and `LlmFixer` fires. The build still saves, but with empty-action regressions in the `.pr`.

### 3. Path-qualify every filter — bare filenames fan out across the whole tree

Each `files` entry **must** start with `system/builder/`. A filter with no `/` (e.g. `"Build.goal"`) is matched by **filename only** — `Builder.Goals` → `MatchesPattern` falls back to `f.FileName.Equals(bf.FileName)` for non-path-qualified entries. `os/` has multiple `Build.goal` and many `Start.goal` files (e.g. `system/modules/db/Builder/Build.goal`, every `<app>/Start.goal`), so a bare list silently pulls in **dozens of unrelated goals** instead of the builder's 8.

Some of those incidental files are stubs or fully `/* … */`-commented (0 parseable steps). The planner then receives a goal whose rendered body is just its name (`"Build\n\n"`) and fails with `BuilderPlannerFailed` ("the LLM never returned a steps array") — a **phantom error that has nothing to do with the builder**. You'll also see a build count far larger than 8 (`builder.actions` firing hundreds of times) — the tell that the filter didn't scope.

Qualify with the **full `system/builder/` prefix**, not a shorter suffix: `MatchesPattern` matches path-qualified filters by `EndsWith`/`StartsWith` *case-insensitively*, so `"builder/Build.goal"` would still match `system/modules/db/Builder/Build.goal` (`Builder` vs `builder`). `"system/builder/Build.goal"` is unambiguous.

## Pre-flight check

Before kicking off a self-rebuild, audit the existing builder `.pr` files. They may carry baked-in mistakes from a previous broken run — and the LLM-driven compile will preserve them via `Kept prior mapping for step N`.

Things to look for:

- **Path stamps wrong:** top-level `path` should be `/system/builder/<Goal>.goal`, `prPath` `/system/builder/.build/<goal>.pr`. Sub-goals share the parent's path/prPath. Anything else (e.g. short `/Build.goal`, `/.build/build.pr`) means a prior build ran with the wrong cwd — rewrite all stamps before rebuilding.
- **`Actor=%goal%` or `Actor=%action%` on `goal.call`:** the LLM mis-parses `foreach X, call Y, item=%var%` and emits `%var%` as the `Actor` parameter on `goal.call`. The runtime then fails at dispatch with `TypeMismatch: Cannot convert app.goals.goal.this to app.actor.this`. Strip every `Actor` entry from `goal.call` parameter lists (none of the builder's calls cross actors).
- **`goal.call` references that don't resolve:** every `goal.call`'s `prPath` (when set) must point at a file that exists on disk relative to `os/`. A stale `prPath` from a renamed sub-goal will surface as `File not found` once the dispatch fires.

A quick Python audit covers all three:

```python
import json, glob, os
for path in sorted(glob.glob('os/system/builder/.build/*.pr')):
    pr = json.load(open(path))
    name = path.split('/')[-1]
    print(name, pr.get('path'), pr.get('prPath'))
    # walk goal.call actions, list Actor params and check prPath existence
```

## Verifying the result

After the self-rebuild succeeds, sanity-check the builder by building something else with it:

```bash
cd Tests/Simple && plang build
```

Should report `Saved Start` with no LLM re-call (`Kept prior mapping for step N` for every step). If that fails, the just-rebuilt builder has a real regression and the change should be reverted before pushing.

## Known LLM regressions — fix upstream, do not strip

These are deterministic LLM mistakes the current prompts emit on self-rebuild. Per the cardinal rule, **do not hand-strip them from the `.pr`**. The fix is in the prompt or the validator. List exists so the next person touching builder prompts knows what to target.

- **`Actor=%goal%` / `Actor=%action%` on `goal.call`** — appears on every `foreach X, call Y, item=%var%` step. The LLM mis-binds `%var%` as the `Actor` parameter of `goal.call` instead of recognising it as the foreach `ItemName`. Fix candidate: tighten the foreach+call examples in `os/system/modules/loop/foreach.examples.md` and `os/system/modules/goal/call.notes.md`, or have the validator reject `goal.call` with `Actor` set and force a retry.
- **`goal.call.Name='goal.call'`** — the LLM occasionally drops the action type name as the `Name` of a `goal.call`'s GoalCall value. The validator's type-name guard catches it and the `FixValidation` retry path in `BuildStep/Start.goal` (the `builder.validate ..., on error call FixValidation` step) re-prompts — usually self-heals on the second pass.

When you fix the prompt so one of these stops happening, delete the corresponding line.

## Related files

- `os/system/Build.goal` — system entry (delegates to `/system/builder/Build`).
- `os/system/builder/*.goal` and `os/system/builder/{BuildGoal,BuildStep}/*.goal` — the builder's own goals (8 files in v3, listed in the recipe above).
- `os/system/builder/llm/Plan.llm`, `Compile.llm` — the LLM prompts (Plan = one-call-per-goal action sets; Compile = one-call-per-step chain).
- `PLang/app/modules/builder/code/Default.cs` — `IBuilder` actions: `goals`, `validate`, `validateStepActions`, `enrichResponse`, `validateResponse`, `goalsSave`, `merge`, `promoteGroups`, `load`, `appSave`, `types`, `actions`.
- `Documentation/v0.2/build.md` — general `plang build` CLI usage (not bootstrap-specific).
- `Documentation/v0.2/build_process.md` — what each builder goal does at runtime.
- `docs/modules/builder.md` — builder module action reference.

---

# Appendix: reliability work

The rest of this file tracks the ongoing effort to make self-rebuild deterministic. Read this if you're touching builder prompts, the action catalog, the validator, or any LLM-facing type teaching.

Self-rebuild of `system/builder` historically produced inconsistent action+modifier shapes for complex steps — different combination of failures every run (goal-name→CLR-type, error.handle.Key holding a goal name, Path expanded to a record, Data wrapped as `{Value, Key}`, etc.).

Shared root cause: the **formal language** (the syntax the LLM thinks in before emitting JSON) and the catalog **Examples** (per-action teaching) underspecify compound parameter values, so the LLM extrapolates JSON-dump conventions from one Example to another. The fix direction is to make each type own its own LLM teaching, then align Examples with it.

---

## Done

### Render `action.Modifiers` in `goalFormatForLlm` template

`system/builder/templates/goalFormatForLlm.template:4` now iterates `a.Modifiers` after `a.Parameters` with `|` separator and the same `Name([type] value)` param syntax as actions. Dead `step.Cache` and `step.OnError` branches removed (those fields don't exist on Step — modifiers live on `Action.Modifiers` per `goals-steps.md:105`).

Helps the **@known re-render path only**: once a step is built correctly once, the LLM sees it on rebuild and reuses it. First-build problems unchanged.

### Structured `ExamplesForLlm()` on action classes

Added `App/Catalog/{ExampleSpec, ActionSpec, ExampleHelpers, ExampleRenderer}.cs` and wired discovery in `Modules/this.cs`. Author writes meaning (`Action("file.read", new() { ["Path"] = "%path%" })`); the renderer derives type tags from reflection on the action class and emits the canonical formal-language string for the catalog's `e.g. ...` line. Migrated `error/handle.cs` as the pilot. Drift between `[Example]` and the type catalog is now structurally impossible for migrated actions.

### Type-owned LLM teaching for Scalar types

`[PlangType]` extended with `Shape`, `Example`, `Description` properties; `TypeEntry` extended with `ConstructorSignature` and `Properties` (read-only navigation fields from `[LlmBuilder]` props). `BuildTypeEntries` detects the `Resolve(input, Context)` static-method convention and emits `path: constructor(rawPath: string), properties: extension(string), fileName(string), ... (e.g. /some/file.json)` instead of just a name. `Path` declares its teaching this way — Shape comes mechanically from Resolve, properties from `[LlmBuilder]`-tagged read-only props.

### Granular LLM debug tracing

`Debug.LlmTrace : bool` replaced with `Debug.Llm : LlmDebug?` carrying `System / User / Response / Schema` sub-flags. Each enabled flag emits its own `=== LLM <PART> ===` block via the standard grep+truncate pipeline. `OpenAiProvider` gained `OnAfterResponse` event (alongside `OnBeforeRequest` which now also passes the schema string). Critical for verifying LLM behaviour without confusing pre- and post-enrichment data — `pass1.response` in trace files runs through `validateResponse` + `enrichResponse`, NOT raw API output. `Documentation/v0.2/debug.md` updated with the new shape and a callout about this distinction.

### `Path`-as-record bug — was C# enrichment, not LLM

`DefaultBuilderProvider.NormalizeParameterTypes` was calling `TryConvertTo("data.txt", typeof(Path), context)` during build-time enrichment, which inflated string parameter values into full `Path` records (Raw, Absolute, Relative, Extension, FileName, ...) that serialized to bloated, non-round-trippable `.pr` files. The LLM was emitting clean strings all along — three sessions of "LLM hallucination" debugging traced to one `TryConvertTo` line.

Fix: skip `TryConvertTo` for Scalar PlangType targets (types with a `Resolve(input, Context)` static method or a `[PlangType(Shape)]` declaration). The string stays a string in the `.pr`; runtime auto-wraps via the source generator's Resolve convention when the action actually executes.

### Build-save validation enforcement

`GoalsSave` now calls `validateResponse.ValidateGoalState(goal)` as the final safety net before persisting the `.pr` — closes the documented-but-unwired hook the validateResponse author had set up.

`Validate` (`builder.validate`) now also enforces required parameters: any property that's non-nullable, has no `[Default]` attribute, and isn't a `[Code]` or capability-interface slot must appear in the LLM-emitted `Parameters` list. Missing required params now produce a `BuildValidation(400)` error that triggers `LlmFixer` / `HandleValidationError` retry instead of slipping through to a saved-but-broken `.pr`. Verified by stress test (`read` step without `Path` correctly aborts the build).

### Schema-driven type tag on parameters

`NormalizeParameterTypes` now always stamps `p.Type` from the action's declared parameter type when available — overriding any LLM-emitted type that disagrees. The LLM tags the value's content shape (`404 → "int"`); the schema tags the parameter's declared CLR type (`Key → "string"`). Schema wins. Value-conversion extended to bidirectional: string→typed and primitive→string both run, so `Key=404 (int)` from the LLM gets normalized to `Key="404" (string)` matching the declared `Data<string>?`.

Side-effect win: `error.handle.Key` filter values now have the right type to match against an actual string error.Key at runtime.

---

## Open items

### ~~1. Type-owned LLM teaching~~ — DONE (see Done section)

Originally:

Today `TypeMapping.BuildTypeEntries` reflects on each `[PlangType]` class: emit enum values, emit `[LlmBuilder]`-decorated properties, or treat as opaque. The framework decides what to teach.

Invert: each `[PlangType]` class declares its own teaching, attribute-based.

**Attribute shape (locked):**

```csharp
[PlangType("path",
    Example = "/some/file.json",
    Description = "Filesystem path. Relative paths resolve against the calling goal's folder; absolute paths start with '/'.")]
```

- `Example` — the canonical value form. Just the value (not the wrapping `Param([type] value)` syntax — the framework adds that). The LLM mimics this directly when emitting both formal and JSON.
- `Description` — semantic notes the LLM needs that aren't visible from the value alone (resolution rules, surprises, what-this-isn't).

Field-level teaching uses the same `Description` slot on `[LlmBuilder]`, so a property like `error.handle.Key` can clarify "filter pattern matching `error.Key`, not a goal name" without renaming the field. Type-level and field-level teaching stay parallel.

For types where the value is a record (e.g. `goal.call` if we keep it as `{name, parameters?}`), `Example` shows the JSON literal form — the LLM treats that as the canonical shape and won't expand it via reflection. For types that are bare scalars (`path`, `operator`), `Example` is the literal string.

Surprise found while diagnosing: `Path` is already opaque in today's catalog (no `[LlmBuilder]` props on it). Yet the LLM still expanded it to a `{Raw, Absolute, Relative, ...}` record last session. Without explicit type-owned teaching, the LLM falls back to training priors and to extrapolating from other Examples. Explicit teaching closes both holes.

### ~~2. Structured `ExamplesForLlm()`~~ — DONE (see Done section)

Originally:

Replace the free-form `[Example]` string with a structured static method on each action class. The framework derives the rendered formal string from the structure, consulting the type catalog from (1) for each value's shape. Drift between Examples and the type catalog becomes structurally impossible — there's one source of truth for how each type renders.

**API shape:**

```csharp
public partial class Handle
{
    public static ExampleSpec[] ExamplesForLlm() => new[] {
        Example("read %path%, on error key 404, write out \"missing\"",
            Action("file.read", new() { ["Path"] = "%path%" },
                modifiers: [
                    Action("error.handle", new() {
                        ["Key"] = "404",
                        ["Actions"] = new ActionSpec[] {
                            Action("output.write", new() { ["Data"] = "missing" })
                        }
                    })
                ])
        )
    };
}
```

- `ExampleSpec` — `(UserIntent, Chain[])`. Multi-example: return an array.
- `ActionSpec` — `(Module, Name, Params, Modifiers?)`. Composes recursively; nested action-list values are `ActionSpec[]` in `Params`.
- `Example(...)` / `Action(...)` — helper constructors keeping author-side syntax compact. `using static App.Catalog.ExampleHelpers;` in the action file.

**Author writes meaning. Framework writes syntax.** No `[path]`, no `Param(...)`, no JSON dumps in author-facing code. The renderer walks each `ActionSpec`, looks up each parameter's CLR type from the action class, finds the type's `[PlangType]` `Example`/`JsonShape`, and emits the canonical formal string.

**Multi-example.** Some actions need several examples — the array form covers it.

**Optional.** Not every action needs one; simple actions are self-explanatory. The framework treats absent `ExamplesForLlm()` as "no examples." During transition, `[Example]` keeps working for not-yet-migrated actions; both can coexist.

**New pieces this introduces:**
- `App/Catalog/ExampleSpec.cs`, `ActionSpec.cs`, `ExampleHelpers.cs`
- A renderer that walks an `ExampleSpec` → formal string, consulting the modules registry (for parameter types) and the type catalog (for value shapes)
- Discovery: the catalog generator reflects for `static ExamplesForLlm()` on `[Action]`-attributed classes

### ~~3. Enforce build-save validation~~ — DONE (see Done section)

Originally:

The validation gap is structural, not a one-off. Two confirmed cases:
- **Last session**: `ValidateBuildResponse` rejected the LLM output (goal-name → CLR-type), but the build saved anyway. Validation logged the error and the cascade carried on.
- **Builder source itself**: a `save app` step exists with no `%app%` parameter passed in. That should be a build-time validation failure (required parameter missing) — yet the builder built it without complaint and the runtime is somehow tolerating it.

These say required-parameter / shape validation isn't enforced on save. Whatever validation exists logs warnings without halting persistence, and some required-parameter checks may not run at all.

Scope:
- Trace where `BuildStep` / `Build` / `BuildGoal` decide to persist a `.pr`. Identify which validation results gate persistence and which only log.
- Required-parameter check: every action's required parameters (non-nullable, no `[Default]`) must be present in the LLM-emitted parameters list. If missing, fail the step before save.
- Validation cascade contract: if `ValidateBuildResponse` returns errors, save MUST be skipped or the step retried. No "logged + saved anyway" path.

This unblocks self-rebuild as much as the formal-language fixes do — they solve "LLM emitted the right shape", this solves "we don't accept the wrong shape."

### 4. Modifier vs peer-action separator (deferred)

In the formal language `|` means two things — "next peer action of this step" AND "modifier on the preceding action." Today the LLM disambiguates because only `[Modifier]`-decorated actions can appear in modifier position. Structurally ambiguous but not currently causing failures we can attribute to it.

Defer: see if (1)+(2) alone make self-rebuild reliable. Revisit only if shape failures continue around the modifier boundary.

### 5. `error.handle.Key` rename (deferred — user disagrees)

I argued `Key` is opaque as a filter-pattern field name. User disagreed, wanted to wait. Tracking only — not in execution plan unless re-opened.

---

## Out of scope (separate concerns)

- **Path/PrPath context loss.** Path/PrPath reverted to `/Build.goal` form (without `/system/builder/` prefix) on rebuild last session. `Goal.Path` and `Goal.PrPath` are plain `string?` fields per `goals-steps.md:14`, not `Path` domain objects — so `[PlangType("path")]` on `Path` doesn't reach them. Likely fix is field-level `[LlmBuilder(Description = "absolute from app root, e.g. /system/builder/Build.goal")]` on `Goal.Path` plus making sure prior Goal.Path is shown to the LLM on rebuild (analogous to the modifier-rendering fix). Revisit after item 1.
