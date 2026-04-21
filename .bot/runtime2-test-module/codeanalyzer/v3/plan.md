# Codeanalyzer v3 Plan — Re-review of coder fix `d05c138d`

## Context

Coder addressed my v2 must-fix finding. Commit `d05c138d` "Address
codeanalyzer v2 must-fix: propagate Step to inner elseif actions",
+8 / −4 LOC across 2 files.

The coder went beyond the minimal `?? true` → `?? false` patch I suggested
and **fixed the root cause**: `SplitAtConditions` now reads via `this[i]`
(the Actions indexer) instead of `_items[i]`, so the lazy `a.Step ??= Step`
propagation reaches every action returned to `Orchestrate`. The `?? false`
change is kept as belt-and-suspenders.

The commit message identifies **two additional latent bugs** with the same
root cause (inner action.Step == null) that I missed in my v2 review:
1. `alreadyOrchestrating` guard-key mismatch — inner's null Step hashed to
   a different key than the outer's. Today masked by the `actions != null`
   short-circuit falling through to simple path anyway, but a real latent
   bug.
2. `DisableChildrenOf` silently skipped for inner elseifs — meaning
   sub-steps stayed disabled even when an inner branch evaluated true.

## Scope

Tiny delta — 2 files, 11 LOC of change:
- `Action/this.cs:67` — `?? true` → `?? false`
- `Actions/this.cs:105, 110, 116` — `_items[i]` → `var action = this[i]` +
  XML doc note on why

## Review approach

Focused passes, scoped to the fix.

### Pass 0 — Verify the regression is resolved

1. Does `SplitAtConditions` now return actions with Step set? Read
   `this[i]` → `a.Step ??= Step;`. Is `Actions.Step` guaranteed set at
   the time `Orchestrate` calls `SplitAtConditions`?
2. For the v2-regression trace (inner elseif firing): does
   `action.IsFirstConditionInStep` now return `false` (correctly excluded
   by the coverage filter)?

### Pass 1 — Verify the two additional latent bug fixes

1. **alreadyOrchestrating guard** — Trace: inner elseif's `userStep =
   __action.Step`. Now non-null. Same step as outer → same GetHashCode →
   same guardKey → `Context.Get<bool>(guardKey)` returns the outer's
   `true` → inner falls to simple path via the alreadyOrchestrating path
   (the INTENDED path), not via the `actions == null` accident.
2. **DisableChildrenOf for inner elseifs** — Trace scenarios:
   - outer false, inner_0 true → net: enabled (correct)
   - outer false, inner_0 false, inner_1 true → net: enabled (correct)
   - outer false, all inners false → net: disabled (correct)
   - outer true → inner never evaluated, outer's enabled state holds

### Pass 2 — New issues introduced?

1. Every action returned from `SplitAtConditions` now goes through the
   indexer. Any side effects of the indexer beyond `a.Step ??= Step;`?
   Read the indexer — no others.
2. Is the `?? false` semantic safe at every call site? `IsFirstConditionInStep`
   has one consumer (run.cs coverage subscriber). If Step is null at that
   point (post-fix, unlikely), `?? false` → skip recording. Safer than
   `?? true`.
3. Remaining `_items[i]` uses in Actions.@this — are they all
   internal-only (return bool, not an action)? grep confirms: lines 50
   and 83 check `IsCondition`, don't return/invoke. Safe.

### Pass 3 — Deletion test on the fix

If the three-line indexer change were reverted:
- v2 regression reappears (phantom "?:?" coverage)
- alreadyOrchestrating guard-key bug reappears
- DisableChildrenOf for inner elseifs breaks
- No current test catches any of these (full suite still 2243/2244 both
  before and after). Worth a v3 note: tests should catch this.

## Deliverables

- `v3/result.md` — findings (expect CLEAN)
- `v3/verdict.json`
- `v3/summary.md`
- `v3/changes.patch`
- Cross-session summary update
- report.json session entry

## Expected outcome

PASS. The coder's fix is root-cause and surgical. The two additional
latent bugs caught show genuine behavioural analysis. One v3 note:
existing tests don't cover the multi-action orchestrate coverage path —
recommend a C# test that exercises it.
