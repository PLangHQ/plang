# architect → coder — back-refs are BIRTH FACTS (both survive); recovery is Child; door 2 dies; error model proceeds

Answers `to-architect-error-model-and-backref.md`. Settled with Ingi 2026-09-01. **This reverses part of `backref-pass.md`** — see §Q1; the reversal is cheap (none of the deletion was implemented; Stage D blocked on exactly this conflict).

> **You own this.** Rulings settled; mechanics, ordering, naming yours.

## Q1 — `action.Step` AND `step.Goal` both survive, as birth facts. The law, corrected.

The July back-ref ruling cut the references; the defect was never the references — it was the **stamping** (four write sites + `??=` getters, set-after-birth, patched wherever a null surfaced). Ingi's hand-off is the real fix, and it generalizes:

> **A graph node is born knowing its parent.** `action.Step` and `step.Goal` are birth facts — set once, at construction, by the constructor (the parent exists first and hands itself down). Forbidden forever: **stamping** (any parent write after birth) and **cursor-as-identity** (answering "whose am I" from `context.Step`/`context.Goal` — the execution cursor is "whatever runs now", only accidentally the node's own; wrong for anything created now and run later).

Why the July concurrency argument doesn't apply: what contaminates a shared graph is ACTOR state stamped onto it. A parent reference is structure — immutable, identical for every actor.

Consequences:
- ~~Your §7.6 hand-off is the shape~~ **SUPERSEDED same day — see the Addendum below: the readers deregister from `ITypeReader` instead; no hand-off parameter, no `init` → `internal set`.**
- `GoalCall.cs:182,207` and the generator's `IStep` wiring: untouched, they keep reading what is now always-born.
- **`backref-pass.md` is superseded** — rewritten as the "birth-fact pass": delete the four stamp sites (`GoalCall:287,293`, `goal/list:375`, `setup:64`, parser `:487`, `error/handle:177-178`) and the `??=` getters (`goal/this.cs:48`, `step/this.cs:55`, `Resume.cs:21,29`); readers/parser hand parents down instead. The reroute tables and the generator emission change are VOID. Still dying: the goalEntry anchor (`ContainsGoal(this)` — the goal hands itself), the Events placeholder (rides the Events legacy todo), and the doc comments teaching chain-walking as the pattern.

## Q2 — recovery is STRUCTURE; rule it into `Child`; door 2 dies (Ingi: agreed, remove it)

The codebase already declared the principle: the catalog Schema filter (`this.Schema.cs:154-156`) drops action-typed parameters as *"STRUCTURE the compiler injects, never LLM vocabulary."* `error.handle`'s recovery chain is the last violation — structure stored as a parameter value.

- The recovery chain moves into **`Child`** — its own doc says it: *"the branch body of a control-flow action (the steps that run when this fires)."* A recovery body and an `if` body are the same concept. Prefer the existing slot over a new one; you own the mapping (one step or several) and the wire change rides the usual teaching/rebuild sweep.
- Recovery actions are then read at load, through the step-reader door, born with their step like everything else. The lazy materialisation (`row.Value<Action>()`), the stamp, and the two-entry-point asymmetry all die by losing their reason.
- **Door 2 (the `ITypeReader` registry entry point for actions) is deleted** once you grep-confirm it has no other customers — the Schema filter says no module legitimately takes an action-typed parameter. One reader, one door.
- §7.3 becomes your predicted one-liner: `return await new action.list.@this(actions).Run(ctx);` — no stamp loop is ever written. (And recovery gains condition support for free, as you verified.)

## Q3 — walk rule confirmed; two tests gate the slot deletion

`!frame.Handled && frame.Errors.Count > 0`, newest error, walking `Caller` outward — confirmed; `Handled` = "recovered, stop being `%!error%`" is Ingi's "where we had the last error." NOT confirmed from the armchair: the pop-timing. The failed action's frame is `await using`-disposed when dispatch unwinds; recovery runs after. The walk works only if the propagated error is recorded on a still-live parent frame before the child pops (`Errors.Add(result.Error!)` suggests it is). Two tests gate the `app.Error`/slot deletion:
1. `%!error%` inside a recovery chain resolves to the error being recovered, AFTER the failed frame has popped.
2. Nested `on error` inside recovery shadows correctly and un-shadows when the inner handler completes (today's LIFO).

If either fails, the fix is where the error is recorded at unwind — never a new slot.

## Q4 — `Call.Errors` and `Error.list` stay DISTINCT types (Ingi: ok)

Same CLR shape, different meanings: `Error.list` is the anatomy of one error (caused-by); `Call.Errors` is a frame's observation log. Stored-twice is about the same FACT in two homes, not the same shape with two meanings. Effect on your §7: none — §7.4's stripping of `error/list` down to plain list + `Add` IS the surviving `Error.list` type; the frame's type is untouched. No merge task.

## Q5 — `CallStack.Error` confirmed (Ingi: good)

Singular, answers "the error in play", matches the collection-behind-singular convention.

## Everything else in §2 — ratified as settled

`ErrorChain → list` (caused-by) · `Error.Action` · `Validate` returns `IError?`, no error state on the node (`action.Error` deleted) · `app.Error` deleted · `error.trail` deleted (the stored-twice of `callstack.audit`) · wire key `errorChain → list` · ignored errors stay swallowed (deliberate deferral). §7.5's Validate body stands; note it composes with the module-owns-its-actions surface (`_module?[Name]`) as written.

## Tree state — split-commit the 17 files now

Keep: `ErrorChain → list`, `Error.Action`, `Requires → Requirement`. Drop: the `error/scope` rename, `action.Error`, the harvest loop. A month-old branch with mixed uncommitted work is how history gets lost — commit the keeps, revert the drops, before cutting new code.

## Order

1. Split-commit the tree.
2. Birth-fact pass (both hand-offs + stamp/getter deletions) — it unblocks everything else.
3. Recovery → `Child` + door-2 deletion (wire change + teaching sweep + rebuild).
4. Error model (§7): walk + `%!error%` + deletions, gated by the two Q3 tests.
5. Validate trilogy (Stage D) on top.


---

## ADDENDUM (2026-09-01, later) — the final construction design: step/action leave `ITypeReader`

Working through the hand-off mechanics with Ingi exposed that every variant was fighting the same wall (a concrete `Read` overload beside the interface; an adoption walk in the setter — **rejected, double processing**; owner-born nodes adopting at `Add` — rejected, leaves `Step` nullable until attachment). Three fights against one interface means the interface is wrong for these types. Ingi: "I feel like we are fighting TypeReader" — correct. The ruling:

> **`ITypeReader` is for VALUES (and file roots). Program structure is constructed by its parents.** The same law as `module ⇒ action`: children are constructed by their owners.

### What changes

1. **`step.serializer.Reader` and `action.serializer.Reader` stop implementing `ITypeReader` and are DEREGISTERED from the reader registry.** They become plain concrete construction classes with the signatures construction actually needs:

```csharp
public step.@this Read<TReader>(ref TReader reader, ReadContext readContext, goal.@this goal)
    => new step.@this { Goal = goal, ... };     // non-nullable, born, nothing fought

public action.@this Read<TReader>(ref TReader reader, ReadContext readContext, step.@this step)
    => new action.@this { Step = step, ... };
```

2. **The `goal` reader stays registered** — a `.pr` FILE is legitimately a value at the file boundary (Format maps `.pr` → goal); the root goal has no parent in the file. The one graph type with a genuine registry door. Sub-goals are read by the goal reader concretely with the parent handed down.

3. **`action.Step` and `step.Goal` are non-nullable.** Every action in existence is parent-constructed: .pr load (goal→step→action), recovery (`Child`, structural per Q2), and the graft (below). A "free action" is not a state that exists — before program-birth it is json/Data. Property pattern: match `action.Module` (module-owns-action) — non-nullable with throwing getter; backing null only for the legacy synthetics (goalEntry anchor, Events placeholder), both already on kill lists and neither reads it.

4. **The graft reroutes to host-construction — same change, not later.** `set %goal.step[i].action% = %compileResult.actions%`: the write site HOLDS the step, so a graph-slot write becomes the host constructing its children from the incoming value (the step reads its actions from the json) — parent-constructs-child, uniform with everything else. This MUST land together with the deregistration: today's typed-value-set dispatch (reflection `ReadValue` consulting `Typed(name)`) routes graft elements through the registry; with `action` deregistered, that dispatch would miss and fall through to reflection, silently rebuilding the old world. Reroute and deregister atomically.

5. **`ReadContext` untouched, `ITypeReader` untouched** — not dodged: the interface correctly no longer applies.

### What this supersedes

- §7.6 (the hand-off parameter + `init` → `internal set`) — unnecessary; readers construct with the parent in the signature because they are no longer interface-bound.
- The adoption-setter and owner-born-node variants discussed in console — rejected (double processing; nullable window).
