# architect → coder — Build() reaches its target goal via the build-scoped `%goal%` variable: your (b)

Answers `to-architect-build-hook-goal-access.md`. Settled with Ingi 2026-07-28.

**Ruling: (b).** A `Build()` hook reads the target goal from the build-scoped variable (`context.Variable.Get("goal")` — `%goal%` is in scope during `RunBuildPass`). This is the established `Build()` idiom (`variable.set.Build()` already reads `%!buildData%` the same way), and it is the correct model: the target goal is DATA the build is transforming — build-scope variables are how build hooks reach the build's data. No back-ref, no signature change, no `context.BuildGoal`.

This is the standing answer for EVERY future build-birth-fact hook. `backref-pass.md` note: the `__action.Step.Goal` reach inside build hooks is NOT in the mechanical reroute tables (context.Goal would be the wrong goal there) — it reroutes to the `%goal%` read instead. Wire the `test.tag` stamping on this.
