# Back-ref pass — delete `step.Goal` / `action.Step` / `GoalCall.Action`; the run carries parentage

The execution plan for the pass ruled in `node-list-wiring-snag-answer.md` (Q2). Runs AFTER node-lists lands. Full consumer sweep done by architect 2026-07-24; every site verified by grep, dispositions below.

> **You own this.** The dispositions are settled; mechanics, ordering within the pass, and naming are yours.

## Why

`step.Goal` and `action.Step` are run-state stored on program structure (the program/run law). The run already carries parentage: `context.Goal`/`context.Step` for the duration, and the Call frame from push. Four stamp sites + two `??=` getters + a defensive parser stamp exist only to keep the back-refs non-null — the same sediment pattern as GoalCall's Convert arms. Deleting the back-refs deletes the wiring loops (the CONDEMNED bridge), the goalEntry anchor, and the Events placeholder's reason to exist.

## What the Call captures (the enabling change — do FIRST)

```csharp
// callstack Push — the frame captures its run-record from context, once, at push:
public call.@this Push(Action action, actor.context.@this context)
    => new call.@this(action, context.Step, context.Goal, ...);
// call.Goal / call.Step — run state on run structure, the correct home (same pattern as
// Error capturing Step at construction and call.Errors — those STAY, they are the pattern done right).
```

Also FIRST: the **source generator emission change** (find #1 below) — without it, deleting `action.Step` breaks every `IStep` handler at generation.

## Demolition — writers (11 sites, all die)

| Site | What dies |
|---|---|
| `goal/this.cs:48` | `s.Goal ??= this` in the Step getter — getter becomes plain |
| `step/this.cs:55` | `a.Step ??= this` in the Action getter — same |
| `goal/this.Resume.cs:21,29` | resume re-stamps (`??= this`) — resume IS the goal |
| `GoalCall.cs:287,293` | load-wiring loop (condemned bridge site 1) |
| `goal/list/this.cs:375` | load-wiring loop (condemned bridge site 2) |
| `goal/setup/this.cs:64` | setup's copy of the wiring loop |
| `goal/this.cs:487` | `.goal` text parser stamp |
| `goal/this.cs:290` (+ `:322` reader) | goalEntry anchor `goalEntryAction.Step = Step[0]` — cycle check becomes `ContainsGoal(this)`, the goal hands itself |
| `error/handle.cs:177-178` | recovery-chain stamp `action.Step = enclosingStep` — recovery runs in a context that already carries Step |
| `Events.cs:35,38` | placeholder action + `gc.Action` — rides the Events legacy todo; if that todo hasn't run yet, this pass may do the minimal deletion here |

Then the three property deletions: `step.Goal`, `action.Step` (`action/this.cs:97`), and **`GoalCall.Action`** (`GoalCall.cs:47-49` — see find #2).

## Reroute — readers (~14 sites)

### → `context.Goal` / `context.Step` (in-run; context in reach)

| Site | Note |
|---|---|
| `GoalCall.cs:182` | `Action?.Step?.Goal` chain walk → `context.Goal` (GetGoalAsync has the context param) |
| `GoalCall.cs:207` | callerDir → `context.Goal.Path` |
| `modifier/this.cs:40` | already does `Step ?? context.Step` at `:39` — the fallback becomes the only path |
| `cache/wrap.cs:61` | goalPath → `context.Goal` |
| `goal/setup/this.cs:143` | event payload goalPath → `context.Goal` |
| `actor/context/this.cs:467,521-522` | event-binding scope naming, called in-run → context |
| `test/run.cs:113-117,159` | tester runs goals; `:117`'s position (`Step.Action.IndexOf`) → the frame |

### → the Call's captured refs (post-mortem / cross-frame)

| Site | Note |
|---|---|
| `callstack/this.cs:101-102,142` | ContainsGoal + chain walks → `call.Goal.PrPath` |
| `callstack/this.Snapshot.cs:219,225` + `call/this.Snapshot.cs:24-25` | capture/restore → frame's own `Goal`/`Step`; restore keeps the internal positional indexer |
| `error/CallChainRenderer.cs:40-50` | frame render → `frame.Goal`/`frame.Step` |
| `debug/this.cs:305-306` | frame render → same |

### → already captured on the error (storage unchanged; fallbacks die)

`Error` stores `Step` at construction (`Error.cs:187`) — a run-record capturing at birth, correct, stays. The step-form ctor (`:182-188`) collapses into the context form (`:206`) so `error.Goal` is ALWAYS captured; then the fallback chains at `:249` (`error.Goal?.Path ?? error.Step?.Goal?.Path`) and `:381` (`error.Goal?.App ?? error.Step?.Goal?.App`) lose their second arm.

## The three finds not in your original table

1. **The source generator is a consumer.** `module/IStep.cs:5`: "The source generator wires `Step = action.Step` in ExecuteAsync." The `IStep` capability (and everything riding it — `loop/foreach.cs:87`'s `Step?.Action` body slice) is fed FROM the back-ref by generated code. This pass includes a `PLang.Generators` emission change: wire `Step` from the context/frame at ExecuteAsync. Do this before the property deletion or every `IStep` handler breaks at generation. `IAction` (`Action = action`) is fine — the runtime handing the current action is not a stored back-ref.
2. **`GoalCall.Action` dies entirely.** Consumers: chain walks (`GetGoalAsync:182,207` → context), stamp sites (`goal/call.cs:57-58`, `build/code/Default.cs:1156`, `Events.Stamp:38`). With walks rerouted, zero readers remain — it was a private road to the goal. Delete the property, its stamps, its `[JsonIgnore]`.
3. **Doc comments codify the disease.** `module/IAction.cs:6` ("Navigate: `Action.Step.Goal` for the full chain") and `callstack/call/this.cs:33` teach the deleted pattern — rewrite to teach the frame/context route, or the next reader reintroduces it.

## Stays (checked, do not touch)

- `channel/type/goal/this.cs:35` — a different type's own `Goal` (channel goal binding).
- `Error.Step` (captured at construction) and `Call.Action`/`Call.Errors` — run-records, the pattern done right.
- `context.Goal`/`context.Step` set/restore (`actor/context/this.cs:372-373`, `goal/this.cs:264`) — the source of truth this pass promotes.

## Order of operations

1. Generator emission change (`IStep` from context/frame).
2. Call captures `(Goal, Step)` at push; capture/restore/renderer/debug reroute to frame refs.
3. Error ctor collapse + fallback-arm deletion.
4. Reader reroutes to context (table above).
5. `GoalCall.Action` deletion (walks first, then stamps, then property).
6. Writer demolition (the 11 sites) + the three property deletions + goalEntry anchor.
7. Doc-comment rewrites.

## Verify

1. Full rebuild from clean + `plang --test` from `Tests/` (stale-binary trap applies) + C# suite.
2. Snapshot capture → resume round-trip (positional restore now reads frame refs).
3. An error raised inside a modifier/event-handler goal reports the ORIGINAL goal/step (the captured refs), not the handler's — this is the case where `context.Goal` and the old back-ref could diverge; the frame capture is what keeps it honest.
4. `foreach` body-slice behavior unchanged (the `IStep` rewire feeds the same step).
5. Grep gates at the end: `\.Goal ??=`, `\.Step ??=`, `Step\.Goal`, `Action\.Step`, `goalCall\.Action` → zero production hits.
