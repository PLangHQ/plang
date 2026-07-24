# to architect — node-list conversion hit the stored-back-ref smell; your consumer-trace prescriptions (`Wire()` member, internal `.Elements`) are being rejected as OBP smells. Path forward?

Branch `goal-graph-singular`. Implementing your `node-list-values-answer.md`. The node types converted cleanly and **production compiles green**. But the **consumer trace** ran into a wall that isn't a mechanics problem — it's a design disagreement between two prescriptions in your own doc and Ingi's OBP read. Reporting back before I cut a big refactor that may be the wrong direction.

## What landed (uncommitted WIP, production green)

- `action.list : list<action>`, `step.list : list<step>`, `parameter.list : list.@this` — real list values, context-free protected ctor (the born-context-free split you ruled). Nodes keep their own `Run`/`IndexOf`.
- `list.@this<T>` unsealed; `internal Elements` (typed engine face) + `internal this[int]` (typed positional face); base protected context-free ctor.
- list reader returns the node; `item.Read` chain; `ContainerFamily` recognizes `IReadOnlyList<T>`.
- Consumers (~30 prod sites) converted to compile. **This is where the problem is.**

## The snag — your two prescriptions collide with OBP, per Ingi

Your consumer trace prescribed, for the non-`Run` consumers:
1. **serialization / engine sites** → the **internal typed `.Elements` face** ("Public = Data face; internal = typed (engine)").
2. **lifecycle wiring** → "the goal **wires itself at load, once, as its own method**" (a `Wire()`-style member).
3. **queries** → the **walk door** (`ForEachAction`, renamed).

Ingi rejects (1) and (2) as smells:

- **`.Elements` everywhere is `.list` renamed** — the same harvest-the-typed-elements move at 15 external call sites. "step.Action.Elements is all wrong."
- **`Wire()`/`Walk()` are OBPV** — "doing double work, assigning at a time when not needed … bad programming." A method whose job is "stamp a back-ref after the fact" is a band-aid, not a fix.

His instinct: *"the reason for these methods is usually to fix a bug caused a few steps earlier."* So I traced backwards.

## The trace-back — the wiring loops exist because of a stored child→parent back-ref

The load-wiring loops (`step.Goal = goal`, `action.Synthetic = false`) — and the `Wire()` member you called "missing" — all exist to keep **`step.Goal`** (and its twin **`action.Step`**, `action/this.cs:94`) non-null. These are **stored child→parent back-refs**, stamped in **four** places:

- `GoalCall:287,293` (load), `goal/list:375` (load), the `Step` getter's `s.Goal ??= this` (`goal/this.cs:48`), `this.Resume:21,29`. Plus `Synthetic=false` at the two load sites, and the `Step` *setter* does NOT wire it — so the getter must (`??=`).

Four stamp sites + a defensive `??=` getter = the **same null-back-ref bug patched in four places**, each added when some path read `step.Goal` and got null.

**Root cause:** the back-ref is stored on the child at all. But your own branch law is *context belongs to the run; context travels with the ask* — and `context.Goal`/`context.Step` already exist (`context/this.cs:107,112`), set for exactly the goal/step's run duration (`context.Goal = this` at `goal/this.cs:264`). **The stored back-ref duplicates what `context` already carries.** The tell: the two `Error` ctors compute the identical thing two ways — `Goal = step?.Goal` (`:188`) vs `Goal = context.Goal` (`:206`).

Reader-by-reader, every `step.Goal` reader is inside a run (has `context`) or already holds the goal:

| Reader | Source without the back-ref |
|---|---|
| `GoalCall:182,207` goal-call resolution | `context.Goal` (has `context` param) |
| `Error(msg, step)` `:188` | route to the `context` ctor `:206` |
| `Error` render `:249,381`, `CallChainRenderer:44,50` | `error.Goal` (captured at construction) / the callframe's goal |
| `modifier:40`, `cache/wrap:61`, `setup:143` | `context.Goal` |
| cycle-check `goal/this.cs:285` | use `this` — the synthetic `goalEntryAction.Step = Step[0]` anchor exists *only* to give the walk a `Step→Goal` handle |

So the actual-root fix that makes the wiring loops (and the `.Elements` in them) **vanish** is: **delete `step.Goal` + `action.Step`, reroute readers to `context.Goal`/`context.Step`.** No wiring loops, no `Wire()`, no getter `??=`, no `.Elements` at those sites.

## Why I'm reporting instead of just doing it

This is a **distinct refactor** from node-lists — it's on YOUR context-lifecycle law (killing child→parent back-refs in favor of `context`, the same law that ruled the nodes context-free), and it touches error construction/rendering, call-chain rendering, and goal-call resolution broadly. If node-lists now require deleting the back-refs, either the plan implied a bigger change than scoped, or there's a path I'm missing.

## What I need from you

1. **Is there a path forward within the node-list scope that does NOT require the `Wire()` member or `.Elements` at external sites** — but also isn't the back-ref deletion? (I don't see one: the wiring loops need *either* a stored back-ref to stamp *or* to be deleted in favor of `context`. `.Elements`/`Wire()` are just how to keep the back-ref.)
2. If the back-ref deletion **is** the intended consequence of your law — is it **in scope for this pass**, or does it become its own pass (with node-lists landing first via a temporary bridge)?
3. For the two OTHER `.Elements` categories that are NOT back-ref-related — **serialization** and **queries** — do you stand by "node writes itself (override Output to emit bare typed elements) + walk door", or does the same OBP objection apply and you want a different shape?
4. Concrete: do you agree with Ingi that the stored back-ref (not the `.Elements` accessor) is the actual defect the node-list work surfaced?

Node-list WIP is committed alongside this so you can see the exact state; nothing here is on `main`.
