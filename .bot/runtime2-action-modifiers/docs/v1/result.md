# Docs v1 — User-Visible Change Notes

Use these bullets directly in release notes when this branch ships.

## PLang developers (writing `.goal` files)

- **`on error` is now per-action, not per-step.** Each action in a step can have its own error handler. Two actions in one step can have two independent `on error` clauses.
- **New match filters.** `on error status 404 ...`, `on error key 'Validation' ...`, and `on error message 'network' ...` restrict the handler to errors that match. Filters combine — all must match. No filters = catch everything.
- **New `cache for ...` clause.** Caches a single action's result for a duration. Success-only storage; failed actions are not cached. Accepts `sliding` for rolling expiry and `key='name'` for explicit keys.
- **New `timeout after ... ms` clause.** Caps a single action's runtime. On deadline the action returns a 408 Timeout error that can be caught with `on error`.
- **Retry + error-goal ordering is explicit.** Default is retry-first (retry N times, then call error goal if still failing). Switch to goal-first with `order='GoalFirst'` — if the goal succeeds, retries are skipped.
- **New `timer` module.** `timer.sleep ms=N` for delays, `timer.start name=X` / `timer.end name=X` for simple stopwatch measurements.
- **Errors pass through as `%!error%`.** When an error goal is called, it receives the original error as `%!error%` (not as a context variable) — so `%!error.Message%`, `%!error.Key%`, `%!error.StatusCode%` are all available.

## Breaking changes for PLang developers

- **Step-level `onError` / `cache` / `timeout` JSON properties no longer exist in `.pr` files.** `.pr` files from older builder versions must be rebuilt — run `plang p build`.

## Module authors (writing C# handlers)

- **New `IModifier` interface.** Implement `Func<Task<Data>> Wrap(Func<Task<Data>> next, Context context)` and mark the handler class with `[Modifier(Order = N)]`. The module is auto-discovered; no other wiring needed.
- **Clone, don't mutate, shared `GoalCall` instances.** Deserialised `GoalCall` objects are shared across invocations. Any handler that needs to set `Parameters` or `Action` on a goal call must clone first (see `PLang/App/modules/error/handle.cs:CallErrorGoal` for the pattern).

## Removed

- `Step.OnError`, `Step.Cache`, `Step.Timeout` properties
- `ErrorHandler` class, `cache.check` / `cache.store` / `error.check` actions
- `RunActionsWithTimeout`, `HandleErrorAsync`, `Retry`, `CallErrorGoal` helpers on `Step`
