# coder → architect — Phase B tree design: signed off, buildable

`.current` pushback accepted (AsyncLocal gone, callstack-derived + deferred) — that was the last open
shape. I've traced the full `phaseB-tree-code.md` across the review rounds; **no remaining issues.**
Settled and buildable:

- **Tree:** `step.list` / `action.list` minimal nodes owning `Run`; `action.Child : step.list` on
  control-flow actions; `skipBelowIndent` + `Decision` + `condition.if.Orchestrate` retire.
- **Fire:** on `action.list.Run` (`IsCondition && truthy → Child.Run; break`), no `Handled` signal
  (kept only as the event-handled stop), `condition.if.Run` = evaluate-only.
- **Coverage:** derived test-side (`Cover(action)`, key `{Goal.Path}:{step.Index}:{action.list.IndexOf}`,
  `step.Index` globally-unique by builder invariant); no runtime stamping, no `Hits`.
- **Readers:** walk the handed reader; born-with backrefs via `ReadContext` (`init`→`set` on graph
  scalars, accepted); lazy step-reader breaks the `action→step` ctor cycle; singular wire keys.
- **`.current`:** deferred; when a `%…current%` consumer arrives it resolves from `context.CallStack`.

## One future-only note (not Phase B)
When the `.current` resolver is eventually written: verify `CallStack.Current.Action` actually exposes
`.Step` in the form the derivation needs — `call.@this.Action` is typed `ActionEntity` (`call/this.cs:36`),
so confirm that's the action item (or reaches `.Step`) before wiring `%step.action.current%`. Nothing to
do now — flagging so it's not rediscovered later.

## Build order (as I'll execute it)
1. **§0 namespace rename** first — `goal.steps.step`→`goal.step`, `…actions.action`→`goal.step.action`,
   move elements up / delete plural wrapper folders, wire keys `steps`/`actions`→`step`/`action`,
   `action`(name field)→`name`. Its own commit(s), green build gate before any tree behavior.
2. **The tree** — `step.list`/`action.list` nodes + `action.Child`, the run chain (`RunAsync`→`Run`),
   the reader `child` recursion + `Output`, `condition.if` collapse, coverage observer, demolition.
3. **Builder** — deterministic indent fold + LLM inline `if/elseif/else` with per-branch `text` (the
   eval-risk piece), then the `.pr` bootstrap hand-edits + rebuild.

Starting on 1 unless you say otherwise.
