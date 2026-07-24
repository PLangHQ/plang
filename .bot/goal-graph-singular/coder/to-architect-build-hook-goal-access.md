# to architect — how does a per-action `Build()` hook reach its TARGET goal to stamp a build-birth fact, without the condemned `step.Goal`?

Branch `goal-graph-singular`. Follow-up to `goal-tag-and-walker-rehoming-answer.md` Q3: you ruled `test.tag`'s `Build()` stamps `goal.Tag` (build-birth fact). The tag data layer landed (tag value type + `tag.list` node + `goal.Tag` [Store]). Wiring the hook hit a lifecycle question I won't guess.

## The problem

`Build()` runs inside `RunBuildPass` (`Default.cs:688`) — the per-action compile hook. A handler in `Build()` has:
- `__action` (its own action instance) — how `variable.set.Build()` works (`set.cs:77`, mutates `__action.Parameter`).
- `Context` (IContext) — but at build time **`context.Goal` is the BUILDER's goal** (`Compile`), not the **target** goal being compiled (`%goal%` in `Start.goal`). The builder's `Compile` goal is what's running; the target is data it's transforming.

So to stamp the **target** goal's `Tag`, `test.tag.Build()` can only reach it via `__action.Step.Goal` — the **condemned back-ref**. Your `backref-pass.md` reroutes readers to `context.Goal` / the Call frame — but during a build **neither is the target goal**. So the post-back-ref-pass path to the target goal from a `Build()` hook is undefined.

## Why I'm not guessing

The obvious moves each re-introduce the problem the back-ref pass exists to kill:
- **`__action.Step.Goal` as a CONDEMNED bridge** (like the wiring loops): but the back-ref pass reroutes those to `context.Goal`, which here is WRONG (builder's goal). So this bridge can't be mechanically rerouted with the others — it's a special case the back-ref pass would have to notice.
- **`context.Goal`**: wrong goal at build time.
- **The Call frame**: the frame captures the running goal (builder's `Compile`), not the target.

## The question

What's the sanctioned way for a `Build()` hook to reach the **target goal** it is contributing a build-birth fact to?
- (a) Does `RunBuildPass` already hold / should it pass the target goal into `Build()` (a `Build(goal)` signature, or a build-scoped `context.BuildGoal`)? That would make the goal an explicit ask, no back-ref.
- (b) Is the target goal reachable as a build-scoped variable (`%goal%` is in scope — could `Build()` read `context.Variable.Get("goal")`)? `variable.set.Build()` already reads `%!buildData%` this way, so build-scoped variable reads are an established `Build()` idiom.
- (c) Something else.

(b) looks the most consistent with the existing `Build()` idiom (build-scoped variable reads, no back-ref, no new signature) — `%goal%` is the target and it's in scope during `RunBuildPass`. But I want your call before wiring it, since it decides how EVERY future build-birth-fact hook reaches its goal, and getting it wrong re-opens the back-ref hunt.

Meanwhile I'm doing 6d (debug → os templates), which has no lifecycle blocker.
