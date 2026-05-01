# auditor v2 — plan for runtime2-data-share-state

## What changed since v1

Coder v3 (commit `b2969406`) responded to my v1 FAIL. The fix path:

- Reframed the contract: events are bound to the **name** (debug watches "x" →
  fires on every assignment to %x%), Properties stay bound to the **Data
  instance** (result metadata like condition.if's branchIndex, not binding
  metadata).
- `Variables.Set` replacement now aliases `prev.OnCreate/OnChange/OnDelete`
  onto `dv` (closes F1).
- `Data.Value` setter fires `OnChange(this)` on direct mutation — closes the
  in-place mutation seam (Variables.Set non-Data path now fires too).
- `variable.set.CarryStateFromSource` deleted entirely — variable.set is pure
  mint+store, Variables.Set owns all state survival. Single source of truth.
- Tests: `SubscriberSurvivalTests` rewritten for the new contract (8 → 11
  tests). New regression test `DebugWatch_OnChange_FiresOnEveryReplacement`
  matches the shape I sketched in v1.

What stayed the same: the F2/F3/F4 carryovers from prior reviews. v1 had
suggested F2 ride along; coder didn't. F3/F4 explicitly noted as
"can ride or wait."

## What I'll check

1. **Verify the F1 fix actually traces the seam.** The unit test mirrors
   `Debug/this.cs:141-160` exactly — placeholder + subscribers + vars.Set.
   Read both sides to confirm.
2. **Trace the new seams introduced by v3** for fresh regressions:
   a. `Data.Value` setter fires `OnChange(this)` — handlers receive (this, this).
      Different shape than the replacement path's `prev.FireOnChange(dv)`.
      Who depends on observing the prev value? OnTypeChange watches in
      Debug compare oldData.RawValue.Type vs newData.RawValue.Type — would
      see same Data twice on direct mutation, never fire. Is that a real
      problem given who writes via the non-Data path?
   b. Unconditional `dv.OnChange = prev.OnChange` aliasing — strong reference
      chain across replaced Datas. GC implications? Surprise factor for
      callers retaining old prev refs?
   c. `set %x% = %y%` no longer carries Properties. Anyone read
      condition.if branchIndex via `set %result% = %__data__%` flow?
3. **Verify the test ground state claim** (C# 2533/2542, plang 166/166).
4. **Check F2/F3/F4 carryover status** — confirm none were addressed (or
   were they? Re-read the diff).
5. **Write verdict** — pass if F1 fix is structurally sound + pinned + no
   new regressions; carryovers logged but not blocking (consistent with v1
   stance that F3/F4 could wait).

## Out of scope

- Re-checking everything codeanalyzer/tester/v1-auditor already passed on.
  Spot-check only where v3 touched code.
- The 9 honest-stub C# failures and 43 sidelined `.test.goal2` files.
- The DebugSmokeTests + DebugWatchRegressionRepro generator artifacts —
  the latter is a stale source-generator cache file from my v1 deleted
  repro, harmless (no source file backing it).

## Risk if I don't pause for approval

Only writing audit artifacts to `.bot/`. No production code touched.
Final verdict drives next bot, not the merge. Low risk.
