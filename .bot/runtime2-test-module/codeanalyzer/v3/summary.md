# Codeanalyzer v3 Summary

## What this is

Re-review of coder commit `d05c138d` "Address codeanalyzer v2 must-fix:
propagate Step to inner elseif actions". My v2 review flagged one
behavioural regression in the IsFirstConditionInStep property's `?? true`
fallback (inner elseif firings recording phantom `"?:?"` coverage).

## What was done

Four-pass focused review of the 11-LOC fix. All passes clean.

### The coder's fix

Root-cause over symptom-patch:

```csharp
// Actions.SplitAtConditions — the one-character change that matters
for (int i = startIndex; i < _items.Count; i++)
{
    var action = this[i];   // was: _items[i]
    if (action.IsCondition) { currentCondition = action; ... }
    else { currentBody.Add(action); }
}
```

`this[i]` goes through the Actions indexer, which does `a.Step ??= Step;`
— propagating the outer step down to every action returned to
Orchestrate. Rather than patch the `?? true` → `?? false` fallback (my
minimal fix), the coder eliminated the null-Step scenario at its source.

The `?? true` → `?? false` change is kept as belt-and-suspenders: if
something breaks the Step propagation in the future, the filter fails
safely (skip recording) rather than producing phantom records.

### What I missed in v2

The commit message identifies TWO additional latent bugs with the same
root cause. Both are real and both are fixed by the same indexer change.

**1. `alreadyOrchestrating` guard-key mismatch.** The guard key is
`$"..._{userStep?.GetHashCode()}__"`. Pre-fix, inner elseif's `userStep`
was null → guard key was a known literal (null hash). Outer's guard key
was different (outer step's real hash). So `alreadyOrchestrating` was
`false` for the inner — but the `actions == null` short-circuit
(userStep?.Actions when userStep is null) saved the day by falling
through to simple path anyway. Post-fix: userStep is set, guard keys
match, simple path reached via the intended route.

**2. `DisableChildrenOf` silently skipped for inner elseifs.** The
`userStep?.Goal != null` guard in If.Run failed when userStep was null.
The outer condition.if's DisableChildrenOf (based on outer's result) was
the final word on sub-step gating, and since outer-false sets
disabled=true, indented sub-steps stayed disabled even when an inner
branch matched. Post-fix: each branch's DisableChildrenOf fires with its
own result; the last-matching branch wins. Sub-steps gate correctly.

I missed both in v2. The lesson for my memory: when a null-reference /
null-prop chain shows up (like `Step == null`), treat it as a class of
bugs — scan for every call site that depends on the missing value, not
just the one where the symptom showed up.

## Code example

The fix (`Actions/this.cs:103-117`):

```csharp
for (int i = startIndex; i < _items.Count; i++)
{
    var action = this[i];                    // ← the fix
    if (action.IsCondition)
    {
        if (currentBody != null)
            branches.Add((currentCondition, currentBody));
        currentCondition = action;
        currentBody = new List<Action.@this>();
    }
    else
    {
        currentBody ??= new List<Action.@this>();
        currentBody.Add(action);
    }
}
```

The XML doc was updated to explain *why* the indexer is used:
> Reads via the indexer so every returned action has Step propagated —
> callers (condition.if.Orchestrate) invoke these actions and need Step
> set for the alreadyOrchestrating guard, DisableChildrenOf, and coverage
> site keys.

Good comment — captures the non-obvious invariant that justifies the
indexer access.

## Note for tester (not blocking)

No existing test catches any of the three bugs. Full suite passes
2243/2244 both before and after the fix. Recommend adding a C# test that:
- builds a multi-action orchestrate step (outer if + elseif + elseif)
- attaches the production coverage subscriber without a ReferenceEquals
  filter
- asserts no `"?:?"` site is recorded
- asserts indented sub-steps execute when any branch matches

That would lock in the fix and catch future regressions across all three
code paths.

## Deliverables

- `v3/plan.md` — approved plan
- `v3/result.md` — per-pass findings
- `v3/verdict.json` — pass
- `v3/summary.md` — this file
- `v3/changes.patch` — analyzer makes no code changes
- Cross-session `.bot/runtime2-test-module/codeanalyzer/summary.md` updated

## What's next

Recommendation: **tester** runs next. The fix is ready. Coverage of the
multi-action orchestrate path would ideally be added so these bugs
can't come back silently.
