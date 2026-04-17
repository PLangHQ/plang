# Docs v1 — Action Modifiers Plan

## Context

Branch `runtime2-action-modifiers` introduces the **action modifier** feature: `onError`, `cache`, and `timeout` stop being step-level special cases and become regular actions distinguished by a `[Modifier(Order = N)]` attribute. Step loses `OnError` / `Cache` / `Timeout` properties; `ErrorHandler.cs`, `CacheSettings` step-level usage, `cache.check`, `cache.store`, and `error.check` are deleted. New modifier modules: `cache.wrap`, `error.handle`, `timeout.after`. New helper actions: `timer.sleep`, `timer.start`, `timer.end`.

Auditor v2 PASS. Security v1 PASS with 3 low hardening items accepted as backlog.

## Gaps identified

### XML doc comments (C#)

Most new public surfaces already have XML docs (`IModifier`, `ModifierAttribute`, `Action.Modifiers`, `Action.WrapAround`, `Modifiers.RunAsync`, `Actions.GroupModifiers`, `cache/wrap.cs`, `error/handle.cs`, `timeout/after.cs`, `timer/sleep.cs`). Missing:

- `timer/start.cs`, `timer/end.cs` — no class-level XML doc
- `ModifierAttribute.Order` property — undocumented (class doc explains it, but property itself is bare)

### User-facing docs (`docs/`)

- `docs/modules/error.md` — the `on error` section still describes step-level error handling. Needs rewrite to document `error.handle` modifier with match filters, retry, goal call, ignore, ordering.
- `docs/modules/cache.md` — **MISSING**. New `cache.wrap` modifier, needs user-facing doc.
- `docs/modules/timeout.md` — **MISSING**. New `timeout.after` modifier.
- `docs/modules/timer.md` — **MISSING**. New `timer.sleep` / `timer.start` / `timer.end` helpers.
- `docs/modules/index.md` — must add entries for cache, timeout, timer modules.

### Architecture docs (`Documentation/v0.2/`)

- `architecture.md` — Step block lists `.Timeout`, `.OnError`, `.Cache` (all deleted). Action block missing `.Modifiers`. Section "Error Handling" mentions `ErrorHandler` class (deleted). Needs update.
- `execution-flow.md` — Sections 2 and 6 reference `error.check`, `cache.check`, `cache.store`, `step.OnError` — all deleted. Must be rewritten to reflect per-action modifier fold.
- `goals-steps.md` — Step properties table has `OnError`, `Cache`, `Timeout` rows (all deleted). Missing the Modifiers concept. Action properties table is also stale on other axes (pre-existing); fix only the modifier-related staleness.
- `good_to_know.md` — "GoalFirst Retry Behavior" note references `Step/Methods.cs:HandleErrorAsync()` (deleted). Needs to point at `PLang/App/modules/error/handle.cs` instead. Add new section: "Action Modifiers — Right-to-Left Fold" covering the design (onError/cache/timeout are actions now, grouped at build, folded at runtime) plus the Shared-GoalCall clone rule (auditor F1 major fix worth recording as a pattern).
- `build_process.md` — Step Properties table has `onError` / `cache` (deleted step-level). Add the new `modifiers` field on Action and note that modifiers are grouped in the save pipeline.
- `building_plang_tests.md` — section on "Common LLM failures" references `onError` JSON property and "`onError`/`cache` present when step has modifiers" — needs update to describe modifier-as-action expectations.

### CHANGELOG / user-visible changes

No CHANGELOG file in repo. Record user-visible changes in `result.md` so release notes can lift them directly.

### PLang examples

`tests/modifiers/*.test.goal` already has six example goals. They're tester-owned; I won't write `.goal` examples for docs, but I **will link** to them from each user-facing module page and include inline snippets (non-executable illustrations of syntax) — the module docs already follow this pattern.

## What this version will do

1. **Write user docs** (new and updated):
   - Rewrite `docs/modules/error.md` to describe `error.handle` modifier with filters, retry, goal call, and ordering. Keep `error.throw` section.
   - Create `docs/modules/cache.md` — `cache.wrap` modifier with DurationMs, Sliding, Key.
   - Create `docs/modules/timeout.md` — `timeout.after` modifier.
   - Create `docs/modules/timer.md` — `timer.sleep`, `timer.start`, `timer.end`.
   - Update `docs/modules/index.md` — add the three modules to the I/O or new "Control flow" bucket; explain the modifier pattern briefly.

2. **Update architecture docs**:
   - `Documentation/v0.2/architecture.md` — replace stale Step block, add Modifiers to Action block, rewrite Error Handling section.
   - `Documentation/v0.2/execution-flow.md` — rewrite sections referencing deleted `error.check` / `cache.check` / `cache.store` / `step.OnError` to describe the modifier fold.
   - `Documentation/v0.2/goals-steps.md` — remove OnError/Cache/Timeout from Step table, add Modifiers row to Action table, mention the fold.
   - `Documentation/v0.2/good_to_know.md` — update GoalFirst note to point at new file, add "Action Modifiers — Fold + Grouping" and "GoalCall clone-not-mutate" notes.
   - `Documentation/v0.2/build_process.md` — update Step/Action property tables.
   - `Documentation/v0.2/building_plang_tests.md` — update references to the modifier concept.

3. **Fill remaining XML doc gaps** in C# (small):
   - Add XML summary to `timer/start.cs` and `timer/end.cs`.
   - Add XML summary to `ModifierAttribute.Order`.

4. **Write reports**:
   - `.bot/runtime2-action-modifiers/docs/v1/summary.md`
   - `.bot/runtime2-action-modifiers/docs/summary.md` (bot root)
   - `.bot/runtime2-action-modifiers/docs/v1/result.md` with user-visible changelog entries
   - `.bot/runtime2-action-modifiers/docs-report.json`
   - `.bot/runtime2-action-modifiers/docs/v1/verdict.json`

## Flag, don't fail

- **Security hardening backlog** (Findings 2–4): negative Ms validation, RetryCount ceiling, ConcurrentStack for cancellation. Not docs gaps — record as "known latent issues" in `good_to_know.md` so future work notices them, but don't fail verdict.
- **Pre-existing staleness in `goals-steps.md`** (e.g., `App.Core.Goal` vs `App.Goals.Goal.@this`, `Libraries` vs `Modules`): out of scope for this branch. I'll fix only what this branch changed.
- **`.goal` example files**: Already exist under `tests/modifiers/`. I'll link to them, not rewrite.

## Quality check

After writing, verify:

- `modules/index.md` lists all new modules.
- No remaining references to `Step.OnError`, `Step.Cache`, `Step.Timeout`, `error.check`, `cache.check`, `cache.store`, `ErrorHandler` (as a class) in the docs (`Documentation/v0.2/` + `docs/`).
- Each new user-doc module page has an example matching the actual tests in `tests/modifiers/`.

## Verdict

On success: `pass` — branch ready to merge.
