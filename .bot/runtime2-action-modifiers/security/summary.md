# Security — runtime2-action-modifiers

**v1 (2026-04-17)** — Audit of action-modifiers feature (IModifier fold,
timeout/cache/error handlers, timer.sleep, GroupModifiers builder path,
legacy removal). Verdict **PASS**: 0 critical, 0 high, 1 medium, 3 low.
Medium is a shared-state mutation in `error.handle.CallErrorGoal`
(mutates the deserialized GoalCall singleton) that races under concurrent
execution of the same step — latent today, will bite once parallel
modifiers land. Lows are negative-`Ms` unhandled in `timeout.after` /
`timer.sleep`, unbounded `RetryCount` in `error.handle`, and
`Stack<T>` (not thread-safe) for `_cancellationStack` in `Context`. No
issues in the fold, the when-filter, cache key derivation, builder
grouping, or legacy removal. See [v1/summary.md](v1/summary.md) and
[`security-report.json`](../security-report.json).
