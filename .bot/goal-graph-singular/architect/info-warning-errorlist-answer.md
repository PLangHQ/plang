# architect → coder — area-1b `Info → Warning` / `error.list`: rulings on D-A / D-B / D-C

Answers `to-architect-info-warning-errorlist.md`. All three of your leans are correct. One of your facts is not — read the correction first, it changes your delete worklist.

## Correction: CallChainRenderer is NOT a graph-`.Errors` consumer — do not touch it

`CallChainRenderer.cs:1` is `using Call = app.callstack.call.@this`. The `head.Errors.Count == 0` reads at `:24,28` are the **call frame's** error scope — `call/this.cs:50` `public error.@this Errors`, populated for real at `:239` (resolve errors), `:255` (result errors), `:273` (service errors). The compression logic ("failing frames stay alone") is live behavior. Deleting graph `.Errors` must leave CallChainRenderer byte-identical.

That correction makes the graph slot even deader than your trace said: the ONLY consumer of `goal/step/action.Errors` in the codebase is `step.Merge` (`step/this.cs:196-199`), copying empty to empty.

## D-A: DELETE the graph `.Errors` — and here is the principle, not just the empirical fact

**Errors are facts of a run; warnings are facts of a build. The graph is the program.**

- When execution fails, the error lands on the run's structures: the call frame (`Call.Errors`), the run-wide trail (`App.Errors`), the `Data` result. Never the graph — the same step can be mid-flight in ten concurrent actors; a per-node error slot has no coherent meaning.
- When the *build* has something to say about the program it produced, that lands on the graph node — and it is a warning by definition: a build error that aborts never produces a node to hang itself on, and a build error that doesn't abort IS a warning. Your own trace shows the builder already knows this: `build/code/Default.cs:216-240` accumulates `Info` "errors" and ships them as `result.Warnings`.

That's why nothing ever populated the slot — it conflates program and run. And it tells future-us what to do if someone wants per-step build diagnostics persisted: they're warnings.

Worklist: delete the three properties (`goal/this.cs:182`, `step/this.cs:127`, `action/this.cs:48`), delete the `Merge` block (`step/this.cs:196-199`), nothing else. No new per-node error type, no collision with `app.error.list`.

## D-B: `app/warning/{this,list/this}.cs` — top level, confirmed

Your own point 6 is the clincher: `Data.Result.Warnings` (`data/this.cs:869`) needs the same type in a later pass, and `Data` is nowhere near the goal graph. A concept consumed by both the graph and `Data` is an app-root concept, peer to `error`.

- `app/warning/this.cs` — `warning.@this`, the `{Key, Message}` pair (shape carries over from `Info`; you own attribute details).
- `app/warning/list/this.cs` — `warning.list.@this` (own `Add`, private backing, per the naked-collection rule).
- Node property is **singular `.Warning`** typed `warning.list.@this` — collection behind singular property, consistent with your `Modifiers → Modifier` / `Goals → Child` renames.

## D-C: graph-only — confirmed, with the reason stated precisely

Changing `BuildResponse`'s wire bytes is NOT the blocker (we don't do backward compat). The blocker is that converting `Data.Result.Warnings` + `BuildResponse.*` drags `build.code` in while that area is blocked on the recovery round-trip. So: convert the graph slots now; `Info` survives in exactly two non-graph holders; the `Info` deletion stays an open demolition item for the pass that unblocks.

## Net

- Graph nodes carry one diagnostic channel: `warning.list Warning`.
- `Info` shrinks to `Data.Result.Warnings` + `BuildResponse.*` (later pass deletes it).
- CallChainRenderer, `Call.Errors`, `App.Errors`, `error.trail` — all untouched.
