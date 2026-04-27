# Docs v1 Summary — Action Modifiers

## What this is

Documentation pass for the action modifiers feature. `onError`, `cache`, and `timeout` are no longer special-case step properties — they are regular actions distinguished by `[Modifier(Order = N)]`, grouped onto the preceding executable action at build time, and folded right-to-left around the action's dispatch at runtime. `Step.OnError/Cache/Timeout` and their handlers (`error.check`, `cache.check`, `cache.store`, `ErrorHandler.cs`) have been deleted. New modules: `cache.wrap`, `error.handle`, `timeout.after`. New helper actions: `timer.sleep`, `timer.start`, `timer.end`.

Auditor v2 PASS. Security v1 PASS with 3 low hardening items accepted as backlog (now recorded in `good_to_know.md`).

## What was done

### User-facing docs (`docs/modules/`)
- **`error.md`** — rewrote the `on error` section to describe the `error.handle` modifier: per-action scope, match filters (StatusCode/Key/Message), retry + retry budget, error-goal call with `%!error%` parameter, `Order` (RetryFirst/GoalFirst), `IgnoreError` fallback. Kept `throw` section unchanged.
- **`cache.md`** (new) — documents `cache.wrap` modifier with DurationMs/Sliding/Key parameters, default key scheme (`step:{goalPath}:{index}`), success-only storage, shared-by-reference foot-gun.
- **`timeout.md`** (new) — documents `timeout.after` modifier: 408 timeout shape, cooperative cancellation, nested composition, parent-cancellation passthrough.
- **`timer.md`** (new) — documents `timer.sleep`, `timer.start`, `timer.end` with parameters and error shapes.
- **`index.md`** — added `timer` to Core and a new "Action Modifiers" bucket listing error (as modifier), cache, and timeout.
- **`builder.md`** — removed `OnError`/`Cache` from the merge-field list; noted Modifiers travel inside Actions.

### Architecture docs (`Documentation/v0.2/`)
- **`architecture.md`** — removed Step.OnError/Cache/Timeout from the entity block; added `Modifiers` to the Action block; rewrote the Error Handling section (the `ErrorHandler` class is gone); added a new **Action Modifiers** section describing the shape, runtime fold, `IModifier` contract, builder grouping, and how to add a new modifier.
- **`execution-flow.md`** — rewrote the run.pr diagram to drop the now-deleted `cache.check` / `cache.store` / `error.check` actions; rewrote Section 6 (Error Flow) around per-action `error.handle` rather than step-level `step.OnError` + PLang `error.check`.
- **`goals-steps.md`** — removed OnError/Cache/Timeout rows from Step properties table; added `Modifiers` row to Action properties table; added a pointer note explaining error/cache/timeout are now per-action modifiers.
- **`good_to_know.md`** — updated the GoalFirst retry note to point at `PLang/App/modules/error/handle.cs` (old file deleted); fixed the `Step.Merge()` field list; removed stale `ErrorHandler` aliases note; added three new sections: **Action Modifiers — Fold + Grouping**, **GoalCall — Clone, Never Mutate** (captures the auditor-F1 shared-state pattern for future modifier/handler authors), and **Modifier Hardening Backlog** (records the three accepted-but-unresolved security findings so they surface the next time anyone touches these files).
- **`build_process.md`** — removed `onError`/`cache` from Step Properties; added `modifiers` to Action Properties; noted that `GroupModifiers` runs in the save pipeline so runtime gets a pre-sorted fold.
- **`building_plang_tests.md`** — reworded the "common LLM failures" and verification checklists from `onError`/`cache` JSON properties to modifier-action grouping; updated the sample `.pr` shape.

### XML doc comments (C#)
- `PLang/App/modules/timer/start.cs` — added class-level summary (stopwatch semantics).
- `PLang/App/modules/timer/end.cs` — added class-level summary including the two ValidationError shapes.
- `PLang/App/modules/ModifierAttribute.cs` — added docstring to the `Order` property listing the current assignments (timeout=1, cache=2, error=3).

### Not written
- **PLang `.goal` examples** — already present under `tests/modifiers/` (`CacheOnFileRead`, `OnErrorCallGoal`, `OnErrorRetry`, `PerActionErrorScope`, `MultipleModifiersCompose`, `TimeoutOnSlowAction`). Linked from each user-doc page. Creating more is the tester's job.
- **CHANGELOG** — no CHANGELOG file in the repo; user-visible changes are recorded in `result.md`.

## Code example — the documented fold

User-facing syntax (from `docs/modules/error.md`):

```plang
- call !RiskyOperation
    on error retry 3 times, call HandleFailure
```

Runtime mapping (from `Documentation/v0.2/architecture.md`):

```
Action("goal", "call") {
  Modifiers: [
    Action("timeout", "after") { Ms = 1000 },       // Order 1 — outermost
    Action("cache",   "wrap")  { DurationMs = 60000 }, // Order 2
    Action("error",   "handle") { RetryCount = 3 }  // Order 3 — innermost
  ]
}
```

`Action.RunAsync` hands its dispatch delegate to `Modifiers.RunAsync`, which folds right-to-left so that first-in-list is the outermost wrapper. Each modifier resolves its own handler via `Action.WrapAround` and wraps the running delegate.

## Verdict

**PASS** — branch is ready to merge. No gaps flagged for coder or tester.

## Build verification

`dotnet build PLang/PLang.csproj` — 0 errors, 836 warnings (all pre-existing generated-code warnings). XML doc additions compile cleanly.
