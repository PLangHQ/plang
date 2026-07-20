# Phase B tree — resolutions to coder's review + the coverage redesign (settled w/ Ingi 2026-07-20)

Answers `coder/to-architect-phaseB-tree-review.md` point by point. Verified every claim against code — the review is sound; accepting the fixes with two rulings that change shape (B2 and the coverage model). Reads on top of `phaseB-tree-design.md`; where they differ, this wins (notably §6 fire and the coverage section).

> **You own this.** Bodies/naming are yours. The settled structure: `action.list.Run` owns chain resolution (no `Handled`), coverage is derived test-side by the existing observer (no stamping, no state on runtime objects), list classes implement `IReadOnlyList<T>`. Comment if a trace breaks any of it.

## Blockers — resolved

### B1 — reader ctor cycle → **accept.** Lazy the sibling reader.
Confirmed: Phase-A readers use `private readonly … _x = new()`; the action reader needing a step reader (`case "child"`) closes `step→action→step` and recurses at construction. Fix: lazy-init the cross-edge (the *action* reader's step reader is the only new back-edge):
```csharp
// action/serializer/Reader.cs
private goal.step.serializer.Reader? _step;
private goal.step.serializer.Reader Step => _step ??= new();   // lazy — breaks the ctor cycle
```
Or resolve from `App.Type.Reader.Reader("step", …)` on demand (the registry option from the read-shape answer). Either is fine — your call; both break the cycle.

### B2 — `Handled` leak → **resolved by dropping `Handled` entirely.** `action.list.Run` owns chain resolution.
You were right to doubt `Handled` (`action/this.cs:162` is its only clear site; it would leak into `%!data%`/`goal.call`). Instead of clearing it, we don't set it. The fire moves off `action.Run` onto `action.list.Run` — the chain owner evaluates, runs the taken branch, and stops, with no signal to carry:

```csharp
// action.list.Run — owns the whole chain resolution
public async Task<data.@this> Run(actor.context.@this context)
{
    data.@this result = context.Ok();
    for (int i = 0; i < _actions.Count; i++)
    {
        var action = _actions[i];
        result = await action.Run(context);                 // condition EVALUATES → bool; non-condition dispatches
        if (result.ShouldExit()) break;                     // return/exit propagates up
        if (action.IsCondition && await result.ToBooleanAsync())
        {
            result = await action.Child.Run(context);       // run the taken branch's body
            break;                                           // skip the rest of the chain (elseif/else)
        }
    }
    return result;
}
```

`action.Run` goes back to plain dispatch/evaluate — **no fire block, no `Handled`**. `condition.if.Run` is just evaluate + `Negate` + return the bool (the whole `Orchestrate` block `if.cs:37-136` deletes). The list reading `action.Child` is not a sibling-reach — it's the chain owner running the branch it owns coordinating (Rule 5); `Child` is the action's own public data. Supersedes design §6.

### B3 — `branchIndex` sibling-blind → **resolved by the coverage redesign (below).** No stamping at all.
Your catch is right — the fired action can't see its chain position, so it can't compute `branchIndex`. The redesign removes the need: nothing is stamped; the test observer derives coverage from what it already receives.

## The coverage redesign — the broken seal comes out

**The smell:** the runtime stamps `branchIndex`/`branchLabel`/`branchChain` into `Data.Properties` (`if.cs:126-128`) and the test observer reads the stamps (`run.cs:109-128`). Coverage — a test-only concern — rides the execution courier. Ingi's rule: test-only → test owns it, and nothing lands on the runtime object (no `Hits` on the action either).

**It's already almost there:** `test.run` already subscribes `AfterAction` for coverage (`run.cs:13,98-101`) and `Coverage` lives in `App.Test`. The observer is already test-side. The only fault is *what* it reads. Make it **derive** coverage from the natural facts it already gets — the action and its own result:

```csharp
// test.run AfterAction observer — derives, reads nothing stamped
childApp.Test.Coverage.RecordModuleAction(action.Module, action.ActionName);
if (action.IsCondition && result != null && await result.ToBooleanAsync())
    childApp.Test.Coverage.Cover(action);        // this branch fired — keyed by the action (its tree position)
```

- The runtime emits **nothing** for coverage — `condition.if` returns its honest bool, which it returns anyway.
- The observer infers "this branch was taken" from `IsCondition` + the condition's own truthy result.
- `Coverage.Cover(action)` records the branch, keyed by the action's identity/tree position (typed, not `site:int`).
- **Declared** branches = the tree walk (`test.discover` enumerates condition actions directly — no `Decision`, no seeded chain). Report = declared minus covered.

**Dies:** `branchIndex`/`branchLabel`/`branchChain` stamping (`if.cs`/`Orchestrate`); the `Data.Properties["branch*"]` reads (`run.cs:109-128`); `Coverage.RecordBranch(site,int)`/`RecordBranchLabel`/`RecordBranchChain` + `_branchIndices`/`_branchChains`; `discover.SeedBranchChains`'s `Decision.Of().Chain` (walk condition actions instead). No `StampBranch`, no `Hits`, no `action.list.Chain`.

**The line:** before, the runtime *computed* coverage and posted it; now the runtime *executes* and the test *observes*. Write and read of coverage both live in `App.Test.Coverage`.

## Should-fix — rulings

- **A1 navigation → accept your `IReadOnlyList<T>`.** The list kind claims `IEnumerable` (`kind/list/this.cs:17`), so implementing `IReadOnlyList<step/action>` makes `step.list`/`action.list` claimed and navigable, one storage, no mutation surface:
  ```csharp
  public sealed class @this : IReadOnlyList<step.@this>
  {
      private readonly List<step.@this> _steps;
      public @this(List<step.@this> steps) => _steps = steps;
      public int Count => _steps.Count;
      public step.@this this[int i] => _steps[i];
      public IEnumerator<step.@this> GetEnumerator() => _steps.GetEnumerator();
      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
      public async Task<data.@this> Run(actor.context.@this context) { /* §B2 */ }
  }
  ```
- **A2 `IsCondition` stays → confirmed.** Needed by `action.list.Run` (chain resolution) *and* the coverage observer (identify a condition firing). It retires only from the fire *gate* (which is `Child.Count>0`… actually `IsCondition && truthy` now — see B2). Keep it.
- **A3 display indent → accept.** `goal.ToText`/`ToString` render from `step.Indent` (`goal/this.cs:68,211`). With the field gone, derive the indent from **tree depth** at render (the depth is the nesting in `Child`). Add to the change list.
- **A4 non-condition `Child` → ruling: `Child` is control-flow only; the fold guards it.** Indentation only follows a control-flow action (conditions; `foreach` calls a goal with no sub-steps per CLAUDE.md). The deterministic fold asserts the preceding action is control-flow before folding indented steps into its `Child`; indented steps after a non-control-flow action are a **build error surfaced**, never silently dropped or run. **Verify against real `.goal` files** — if a legitimate non-condition-nesting pattern exists, bring it back before deleting `skipBelowIndent`.
- **A5 once-truthy gate is condition-only → state as invariant + guard.** `Child` holds branch bodies of run-once conditions; the "truthy → run `Child` once" gate is correct *only* under that. Assert it (a control-flow action with an iterated body would need different handling). The fold + LLM must uphold "only condition actions get a `Child`."
- **A6 `ShouldExit` vs `Returned` → verify at build.** Confirm whether `ShouldExit()` already folds `Returned`; if so, drop the `|| Returned` in `step.list.Run`; if not, keep it and confirm nothing relied on the old `ShouldExit()`-only break.
- **Q4 fold name → your latitude.** A build-pipeline pass, single honest verb or noun-typed; not `TreeBuilder`/`FoldIndent`. `step.Absorb(child)` or extending the existing `Nest` are both fine — settle at build time.

## Net change vs `phaseB-tree-design.md`

- §6 fire: the fire block moves from `action.Run` to `action.list.Run`; `Handled` is gone; `condition.if.Run` is evaluate-only.
- Coverage: no runtime stamping, no `Hits`; the existing test-side `AfterAction` observer derives it via `Cover(action)`; `Decision`/`branchChain`/`RecordBranch`-string-keyed all retire.
- §3 list classes gain `: IReadOnlyList<T>`.
- §7 readers: lazy the action→step cross-edge.
- Demolition adds: the branch-stamping in `if.cs`, the `Properties["branch*"]` reads in `run.cs`, `Coverage`'s string-keyed branch surface, `discover.SeedBranchChains`'s `Decision` use. `IsCondition` moves to the STAYS list.

Everything else in `phaseB-tree-design.md` (the tree model, `step.list`/`action.list` typed storage owning `Run`, the three readers, `Output` recursion, the builder's two producers, `indent` removal, structural merge, acceptance) stands.
