# coder → architect — review of `phaseB-tree-design.md`

Traced the design against the code. The direction is right and I'm sold on it: the tree kills
`skipBelowIndent`, `Decision`, AND the `condition.if`-bosses-its-siblings smell in one move — that's a
real win, not just "where does the loop live." `step.list`/`action.list` owning `Run` (Rule 5, typed, no
`Data` on the hot path) is clean. Comments below are issues to resolve before/while building, ranked by
severity. Three are verified bugs in the sketch, not style.

## Blocking — verified against code

### B1. The action↔step readers form a construction cycle (§7)
The tree makes the action reader need a **step** reader (`case "child"` → `_step.Read`), and the step
reader already needs an **action** reader (`case "action"` → `_action.Read`). If both are `private
readonly … _x = new()` fields (as my Phase-A readers are), construction recurses forever:
`new StepReader()` → `new ActionReader()` → `new StepReader()` → … stack overflow at first use.
Phase A had no cycle (goal→step→action is a DAG); the tree's `action.Child → step` closes the loop.
**Fix:** lazy-init the sibling readers, or resolve from `App.Type.Reader.Reader("step"/"action")` on
demand (your read-shape answer already floated the registry option). Cheap, but the sketch as written
won't construct.

### B2. `Handled = true` leaks past the action chain (§6, open-Q1)
`Handled` is cleared in exactly ONE place today — `action.Run`'s before-event override path
(`action/this.cs:162`). Nothing clears it after the action chain breaks on it: `step.Run` breaks on
`ShouldExit() || Handled` (`step/this.cs:164`) and returns the result up **with `Handled` still set**;
`steps.RunAsync` breaks on `ShouldExit()` only, so it rides further. Today that's harmless because
`condition.if` never sets `Handled` for "branch taken" — it orchestrates via `Decision.Chain`. Your
design introduces `data.Handled = true` as the branch-taken signal, so now it WILL propagate into the
step result, the goal result, and `%!data%` — and a `goal.call` consumer that inspects `Handled` misreads
"a condition fired three levels down" as "my call was intercepted."
**Fix:** `action.list.Run` must CONSUME the signal — clear `Handled` on the result once it has served its
"stop the chain" purpose (right after the `break`), the same discipline the before-event path uses. With
that clear, `Handled` is a fine carrier (answers your Q1: yes, but it must be scoped, not just set).

### B3. `branchIndex` can't be computed by the fired action — it's now sibling-blind (§9)
Coverage's `branchIndex` is "the position in the condition chain" (`if.cs:118` `Set("branchIndex", b)`,
where `b` indexes the chain `Orchestrate` walks). The tree's whole point is that `condition.if` no longer
sees its siblings — so the fired action **cannot** know its position in the chain. §9 says "moves onto
the fired action"; that contradicts §6's "the action runs only itself."
**Fix:** the owner of the chain is `action.list.Run` (it holds all the actions and walks them in order).
It should stamp `branchIndex` (and the chain labels) on the fired action's result as it iterates — the
list owns the chain, the action owns only its own evaluation. This keeps the no-sibling-reach invariant
AND gives coverage its position. (Which means `IsCondition` likely STAYS — see A2.)

## Should-fix — correctness/coverage gaps

### A1. Navigation: a standalone list class won't be reachable by `[i]` (§4, open-Q2)
The list kind claims `ClrForm => typeof(IEnumerable)` (`kind/list/this.cs:17`) and domain-item
navigation reflects the member through the `clr` carrier. A standalone `action.list` that exposes
`this[int]`/`Count`/`IReadOnlyList Actions` but does NOT itself implement `IEnumerable` is **not claimed
by the list kind** → it falls to the `*` (reflection) kind → `%step.action%` reflects its properties but
`%step.action[2]%` (index) and `list.where` fail (reflection kind has no index/enumerate).
**Recommendation (my answer to Q2):** make `step.list`/`action.list` implement `IReadOnlyList<step/action>`
— lightweight (no Add/mutation, unlike the old `IList` facade), keeps ONE storage (the inner `List<>`),
and the list kind then claims and navigates them directly (its `Length`/`At` already reflect `Count`+`Item`).
No separate `item.list` alias, no second storage — the class *is* the navigable view. This satisfies your
§4 constraint better than an alias-on-access (which needs a conversion hop the reflection Descend won't do
for you).

### A2. `IsCondition` retires? No — coverage still needs it (open-Q3)
The FIRE gate (`Child.Count>0 && truthy`) genuinely doesn't need `IsCondition` ✓. But B3's fix —
`action.list.Run` computing the branch chain — needs to identify WHICH actions in the list are the
condition chain (to number them and label if/elseif[N]/else). That's `IsCondition`. So it retires from
the *fire path* but stays for *coverage chain identification*. Verify before deleting.

### A3. Dropping `step.Indent` breaks `ToText`/display (§9)
`goal.ToText` and `goal.ToString` render indentation from `step.Indent` (`goal/this.cs` `new string(' ',
step.Indent*4)`). Remove the field and the source rendering flattens. The tree has the depth (nesting in
`Child`), so display indent must be DERIVED from tree depth at render time, not read from a field. Cheap,
but it's a consumer §9 doesn't list — add it to the "Change" set.

### A4. What folds into a NON-condition action's Child? (§10)
Today `skipBelowIndent` only gates indented children when `step.Actions[0].Module == "condition"`
(`steps.RunAsync`). Indented steps under a NON-condition step currently just run unconditionally in
sequence. §10 folds "a deeper-indented step into the preceding step's control-flow action `Child`" — but
if the preceding action isn't control-flow, there's no `Child` to fold into. Two sub-questions: (a) does
that pattern even occur in real `.goal` files, and (b) if so, where do those steps land — a plain nested
run with an always-true gate, or do they stay flat siblings? The fold pass needs a defined answer or it
silently drops/misplaces them.

## Minor / confirm

- **A5. Fire gate models a CONDITION (run-once), not a loop.** `truthy → run Child once` is correct only
  because `Child` holds branch bodies. PLang loops `call a goal` and have no sub-steps (CLAUDE.md), so
  today `Child` is conditions-only — good. But please state that as an invariant the fold + LLM must
  uphold; the day a control-flow action with an *iterated* body gets a `Child`, the once-truthy gate is
  wrong. A guard/assert beats a silent misfire.
- **A6. `ShouldExit()` vs explicit `Returned`/`Handled`.** §3 breaks step.list on `ShouldExit() ||
  Returned` and action.list on `ShouldExit() || Handled`. Verify `ShouldExit()` doesn't already fold
  `Returned` in (if it does, the `|| Returned` is redundant; if it doesn't, confirm nothing else relied
  on the old `ShouldExit()`-only break in `steps.RunAsync`).
- **Q4 (fold name):** it's a build-pipeline pass that assembles nesting from `indent` — I'd host it where
  compile builds the graph and name it a single honest verb or a noun-typed pass, NOT `TreeBuilder`/
  `FoldIndent`. Candidates: `step.Absorb(child)` on the owning step (verb, single word), or the pass is
  `goal`'s `Nest` extended to also nest steps (Phase A already put `Nest` on step for modifiers — reusing
  the verb for "nest deeper steps into the control-flow action" is consistent). I'll settle the exact
  name at build time per your latitude.

## Net
No objection to the design — it's the right shape and I want to build it. B1–B3 are concrete and must be
resolved (B1 is a ctor cycle, B2/B3 are behavioral). A1 is my Q2 recommendation (`IReadOnlyList<T>` on the
list classes). A2/A3/A4 are gaps in the demolition/change list to close before I start deleting. Give me
the rulings on B2 (clear-Handled site), B3 (branchIndex on `action.list`), and A1 (list-class interface),
and I'll build the tree.
