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


---

## ADDENDUM 2 (2026-09-01, code write-out) — four corrections + the Step nullability ruling

Architect wrote the target code end-to-end against the CURRENT files (all three readers, the properties, the graft, recovery, the walk). Four corrections to the earlier text, one new ruling:

1. **`init` → `internal set` IS needed — Addendum 1 overclaimed.** All three readers today buffer locals and construct the parent LAST; children born holding the parent require the parent to exist FIRST. Goal and step readers flip to shell-first; their scalar `init` props become `internal set`. Your §7.6 cost was real. (`step.Goal` and `action.Step` stay `init` — known at shell birth. The action reader is already shell-first; it only gains the `step` parameter.)
2. **`goal.Parent` is a FIFTH repair site** — `g.Parent ??= this` in the Child getter (`goal/this.cs:56`), same family as the four stamp sites. Shell-first `Walk(ref reader, readContext, parent)` makes sub-goal `Parent` a birth fact; the getter dies.
3. **The graft's dict door takes the parent.** `Step` as pure `init` conflicts with the dict-door chain creating the instance — resolved by the chain growing the parent parameter (`Create(row, data, step)`; the catalog blank-mint `Create()` becomes `Create(step)`), keeping parent-constructs-child uniform instead of weakening the property. The step's own `Set("action", …)` override is the graft door: host constructs children, `Step` in-hand at creation. Lands ATOMICALLY with deregistration (else `ReadValue`'s `Typed("action")` falls through to reflection — which can no longer even resolve `Module`).
4. **Recovery-as-`Child` needs ZERO reader work** — `Populate` already reads `child` on modifiers. The change shrinks to: builder emits recovery under the modifier's `child` key (teaching sweep + rebuild) + `error.handle` runs `__action.Child.Run(context)`; `RunRecovery`/`RunRecoveryWithErrorScope`/the `row.Value<Action>()` door die; `erroredCall.Handled = true` (`handle.cs:112,:129`) stays — it is what the walk reads.

Also confirmed in the write-out: the chain self-feeds (an action's `child` steps get the goal from `step.Goal`, non-null birth fact — no extra threading); and the DICT door's child-step fill (`Made<step>` in `this.Item.cs`) also reads through the registry today — it moves to the concrete reader with the deregistration (same atomic change).

### Can `action.Step` be truly non-null? (Ingi's question)

**For every action in a program: yes, by construction** — every door sets it at birth, all legacy stepless synthetics (goalEntry anchor, Events placeholder) are on kill lists. **For the C# property: not yet** — the blocker is the CATALOG elements: `action.@this` serves two trees (program node with a step; module-minted catalog descriptor with no program and no step). Ruling:
- **Now:** the `Module` pattern — nullable backing, throwing getter, `init`. Reading `Step` on a catalog element throws as the bug it is; nothing reads it there today (catalog consumers read rows/prose/Return), so it is a tripwire, not a landmine.
- **Queued as its own design question:** split the catalog element out of `action.@this` into its own descriptor type — the Schema partial is already that type riding as a partial, and the straddle keeps costing (the killed catalog `Context`, `Cacheable`, `Position`, now `Step`). When that split lands, `Step` goes truly non-null. Not this pass.


---

## ADDENDUM 3 (2026-09-01, after `to-architect-action-step-nullable.md`) — nullable accepted; the seam fix; recovery gets its OWN slot

### Q1 — nullable `Step?` ACCEPTED as shipped
Your razor is correct: `Module`'s throwing getter enforces a true invariant; `Step` has legitimate null states, so the same shape would crash on states the design allows. The enforcement was always the construction doors + deleted repair sites + `internal set` — all landed. Your doc comment naming the stepless kinds is right. Addendum 2 §3's throwing getter is WITHDRAWN. (The 466-failure count was frequency, not variety: sign-if-missing rides every `Wire.Write`, so one seam × every wire-crossing test.)

### The seam fix (Ingi): an `app.Run` invocation is born knowing its CALLER

One line, inside `App.Run`, nowhere else — **all four call sites stay byte-identical**:

```csharp
// app/this.cs
public Task<data.@this> Run<TAction>(TAction handler, actor.context.@this context)
    where TAction : module.ICodeGenerated
{
    var entity = new global::app.goal.step.action.@this
    {
        Module = Module[ResolveModuleName(typeof(TAction))],
        Name   = ResolveActionName(typeof(TAction)),
        Seed   = handler,
        Step   = context.CallStack.Current?.Action.Step,   // ← NEW: born knowing its caller's step
    };
    return entity.Run(context);
    // compose-and-run stays FUSED in this method — the fusion is what makes reading the
    // cursor here honest (no created-now-run-later window). Never split it.
}
```

Worked trace (the sign case):

```
- read file.txt, write to %x%          ← the plang step executing
    └─ file.read dispatches; frame pushed (Call.Action.Step = this step)
        └─ result serializes → serializer calls App.Run(new sign { … })
             Current.Action.Step = the "read file.txt" step   ← sign born with THIS
             └─ sign's frame pushes; cycle check: goalPath == callerGoalPath → correctly quiet
```

- Boot edge: wire write before any goal runs → `Current == null` → `Step` stays null → `Push`'s `?.` answers "no goal boundary", as today. This is why `Step?` stays nullable and stays honest.
- `--debug` bonus: sign/ask frames now render INSIDE their calling step instead of floating stepless.
Why this is NOT the rejected `context.Step` move: the rejection was about PROGRAM actions (created now, run later — displacement makes the cursor a lie). An `app.Run` invocation is **compose-and-run fused** — no displacement window, so the calling frame's chain is definitionally its provenance, same legitimacy as `Call.Push` capturing at push. Two questions, two answers: "which step do I BELONG to" = program fact, birth only; "which step INVOKED me" = run fact, cursor at the fused moment. Keep compose+run fused inside `App.Run` — the fusion is what keeps this honest; never split it into compose-here-run-later.
- Cycle check improves: sign invocations now carry the caller's goal identity; equal paths → the boundary check correctly stays quiet.
- Your Q2 (kill the seam / handlers run themselves) drops from necessary to QUEUED cleanup — it no longer feeds the nullable population. Do not start it.
- `Step?` stays nullable: boot-edge invocations (no caller frame yet) and the description role (below) remain legitimately stepless.

### The three-role taxonomy (Ingi's framing) — the queued split, reframed
One class plays three roles today: (1) **program action** — in a goal, has a step, runs; (2) **description** — the module-minted catalog entry (the Schema partial is already its body, riding as a partial); (3) **invocation** — the C#-composed infra call. Ingi: the description deserves its own type — **DEFERRED, his call, do not start**. When descriptions split out and the seam cleanup lands, role 1's `Step` tightens to non-null `init` honestly.

Refinement (Ingi, examining the signature): role 3 is not a FAKE action — `new sign(_context) { Data = data, StoreView = … }` IS a fully authored action (module via the type's namespace, name via the type, params via the initializer) — a **C#-authored program action** whose authored position is its caller's step. The awkwardness is **one action, two objects**: the record that IS it, plus the `action.@this` entity that re-describes it for the dispatcher (`ResolveModuleName` reflecting a namespace to re-derive a string the caller expressed by naming the type). The queued Q2 cleanup is therefore "one object instead of two" — the source generator already emits a partial on every action record; the run surface (Push/lifecycle with itself) generates onto the record, and `App.Run<TAction>` + its namespace reflection dissolve. Better description, same queued status.

### Q3(a) — my "rule it into Child" was WRONG; recovery gets its OWN structural action-list slot
`Child` holds AUTHORED sub-steps (condition bodies). Recovery is an action chain with no authored step — inventing a wrapper step to satisfy `Child`'s type is the manufacturing we condemned three times. The right shape: a structural **action-list slot on the action**, precedent `Modifier` (already a structural slot of actions on the wire). Payoffs intact: read at load through `Populate` with `step` in hand — recovery actions born with the ENCLOSING step (real, not invented — this also answers "what step do they have"); `action.list.Run` runs the chain (`%!data%` flow + condition support); door 2 still loses its last customer. Slot name: single word, Ingi + you settle; do NOT overload `child` to mean two shapes.

### Q3(b) — no contradiction, my wording was sloppy
`Create(step)` does not give the catalog element a step. The catalog element is the FACTORY; `step` is the PRODUCT's birth fact — "mint a program action of my kind, born into this step." The factory stays stepless.

### Backlog
The Wire-suite stack overflow (`snapshot.serializer.Default.Render` ↔ `data..ctor → type.Create`, pre-existing, aborts the suite) → `open-items.md`. Every Wire number on this branch is from a truncated run — treat Wire results as unknown until fixed.
