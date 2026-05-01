# auditor v2 — summary

**PASS on coder/v3** (commit `b2969406`).

F1 (Debug-watch regression I caught in v1) is closed and pinned. Coder
went structurally cleaner than my suggested partial-revert: reframed
events as bound to the **name**, Properties as bound to the **Data
instance**, made the alias unconditional, deleted `CarryStateFromSource`
entirely. `variable.set` is now pure mint+store; `Variables.Set` owns all
state survival. Single source of truth.

The regression test `DebugWatch_OnChange_FiresOnEveryReplacement` mirrors
`Debug/this.cs:141-160` exactly and asserts 3 sets → 3 calls (was 1).
Plus 6 contract tests pin the underlying behavior (event aliasing,
Properties isolation, idempotent same-instance Set, Value-setter fires).
Verified passing in isolation.

Test ground state: C# 2533/2542 (9 honest baseline stubs unchanged from
v1, +3 new tests in `SubscriberSurvivalTests`, all pass). Plang 166/166.

**Carryovers from v1 (unchanged, not blocking):**
- F2 — `AsCanonical` container-walk transient state-aliasing unpinned
  (`Data/this.cs:491-494`)
- F3 — `global::App.Data.@this.SnapshotClone(...)` qualifier noise
- F4 — defensive `??` fallback can't fire

**One new minor observation (non-regressing):** `Data.Value` setter fires
`OnChange(this, this)` — handlers see same Data twice on direct mutation.
Inconsistent with replacement-path's `(prev, dv)`. Affects only
`OnTypeChange` watches via the `Variables.Set` non-Data path. User
`set %x% = ...` always goes through the Data path, so user-visible
watches work correctly. Logged for awareness, no fix recommended unless
a real consumer needs it.

**Recommended next step:** green-light merge. Optional: fresh security
review on the JSON-roundtrip expansions (still not on file for this
branch).
