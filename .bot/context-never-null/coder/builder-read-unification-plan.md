# Plan: one deserializer for actions — disk `.pr` and LLM step response

**Author:** coder · **For:** architect review · **Branch:** context-never-null
**Related:** `.bot/read-path-unification/` (goal-as-clr final cleanup)

## Problem

An `action.@this` is deserialized by **two different readers** depending on where it
comes from, even though the target type is the same:

| | reader | `%ref%` param handling | path fields |
|---|---|---|---|
| **disk `.pr`** | `goal/serializer/Reader.cs` → `Deserialize<goal.@this>(GoalReadOptions)` | born as **live template** (Wire template mode) | context-wired |
| **LLM step response** | `data.@this<BuildResponse>` param conversion → `CaseInsensitiveRead` (`json.Options.Read()`) | stays a **literal string** | not wired |

`GoalReadOptions` = `Wire.ReadOptions(ReadContext(context, "plang", View.Store, Verify:false))`
(the Data.Wire reader in template mode). `CaseInsensitiveRead` is the generic read.

So a `%x%` step-param born from the LLM ≠ the same `%x%` born from disk. This is a strong
candidate for the born-source `%ref%` failures currently red on this branch (a param that
should be a template is a bare string until the builder transforms + re-writes the `.pr`,
after which runtime re-reads it correctly via `GoalReadOptions`).

## Key facts (verified in source)

- **Compile is per-step.** `os/system/builder/BuildGoal/Start.goal`:
  `foreach %plan.steps%, call BuildStep`. `BuildStep/Start.goal` = "Compile one step".
- **The LLM already returns `.pr`-shaped actions.** `BuildStep` llm.query Schema:
  `actions: list<{module, action, parameters?: list<{name, value?, type?}>}>`.
  That is exactly the `.pr` action node shape.
- **The types are shared both paths.** Global usings: `Step = app.goal.steps.step.@this`,
  `StepActions = app.goal.steps.step.actions.@this`; params are `action.@this`.
- **Typed resolution already happens at build.** `RunBuildPass` (builder/code/Default.cs:~618)
  calls `shell.Resolve(a, context)` → the typed action instance, holding its node as `__action`.
- **`action.@this` already serializes to `.pr`** via its own `Output` (the channel) — no new
  serializer needed to write.

So today: LLM JSON → `BuildResponse` (CaseInsensitiveRead) → validate → enrich → normalize
→ `goal.@this` → write `.pr`. Runtime then reads that `.pr` via `GoalReadOptions`. The extra
options set + transform pipeline is the second path.

## Goal

The LLM's per-step `actions` deserialize into `Step` / `action.@this` through the **same
reader as disk** (`GoalReadOptions`), so `%ref%` holes born identically. Then the resolved
typed action runs `Build()` (sets its own `Type`, typed), and `Output` writes the `.pr`.
**One reader, one writer, one shape.**

## Proposed shape

1. **Read the LLM step through the goal reader.** Parse the LLM's `actions` array into
   `List<action.@this>` (or a `Step`) using `GoalReadOptions` — replacing the
   `BuildResponse` + `CaseInsensitiveRead` parse. `%ref%` params born as templates,
   identical to disk.
   - The LLM response's *other* fields (`formal`, `confidence`, `explanation`, `errors`,
     build-only `Keep`/`Warnings`) are build metadata. Fold onto `Step` (which already
     carries build fields like `PriorText`) or keep a thin `StepBuildResult` wrapper —
     architect's call.

2. **`Build()` on the resolved typed action** (already runs at build) sets its own typed
   props — e.g. `variable.set.Build` adopts the captured type. Setters write through to
   the action's `Parameters` (params become `internal set` + write-through).

3. **Write back via the channel.** `action.@this.Output` → `.pr` node into a MemoryStream,
   exactly like a file write. No new serializer.

4. **Cross-action type inference stays generic** in `RunBuildPass`: after each `Build()`,
   publish its returned Data as `%!build` (infra-scoped, distinct from runtime `%!data%`);
   the next action's `Build()` reads it if it wants (`variable.set` does). Never
   special-cases variable.set.

## What likely collapses once read is unified

- `BuildResponse` and much of `validateResponse` / `enrichResponse` / `NormalizeParameterTypes`
  exist to bridge the LLM shape → goal shape. If the LLM shape is read directly as
  `Step`/`action.@this`, most of that transform layer has nothing left to convert.
- The `StampOnTerminalVariableSet` mechanism (already being replaced by `variable.set.Build`
  + `%!build`) — no string-key param magic.

## Open questions for architect

1. **Where to swap the reader** — at the `data.@this<BuildResponse>` param conversion, or a
   dedicated step-read at the `Compile` boundary in `BuildStep`?
2. **Build-only metadata home** — fold `formal`/`confidence`/`errors`/`Keep`/`Warnings` onto
   `Step`, or a thin `StepBuildResult`?
3. **Typed write-back** — the resolved typed action's node (`__action`) serializes via the
   existing `action.@this.Output`; confirm that's the write path (vs a generated `Output`
   on the typed class). This also decides whether the two objects (`action.@this` node +
   typed class) stay split (bridge via `__action`) or unify (typed class inherits the node).
4. **Value pinning / versioning** — `Build()` writing effective values (incl. explicit
   defaults) makes the `.pr` deterministic across future default changes. In scope here or
   separate?
5. **Interaction with `.bot/read-path-unification/`** (goal-as-clr cleanup) — this shares the
   "one read path" goal; sequence with that effort.

## Non-goals

- Runtime execution path unchanged (already reads `.pr` via `GoalReadOptions`).
- Not unifying the whole goal-tree object model in this pass (question 3 flags it but it can
  stay split).
