# coder handoff — graph→items, increment 1 (action/step) — WIP, NOT committed

Branch `goal-graph-singular`. Implementing the items-answer.md direction (graph becomes plang items).
Two uncommitted files: `app/goal/steps/step/actions/action/this.Item.cs`, `app/goal/steps/step/this.Item.cs`.

## What's done (increment 1: action + step become items, transitional)

- `action` and `step` now `: item.@this, ICreate<@this>` (modifier rides free as `: action`).
- Faces: `Type` (`new("action"/"step", typeof(@this))`), `IsLeaf => false`.
- `Output` **delegates to the reflection (*) kind** — TRANSITIONAL, byte-identical wire. Replaced by
  explicit `Write` + `serializer/Reader.cs` in the next increment.
- `Set` (child-write) override — the base `Get` already reflects (via the clr carrier), but base `Set`
  throws. Added a reflect-set (opens the Data door, lowers `value.Clr(propType)`, returns `this`).
  This is what un-broke the layer-5 write (`set %goal.Step[i].Action% = clr(json)`).

## Verified

- PLang library builds clean (0 errors).
- **Wire + Modules: ZERO delta** vs a proper multi-run baseline (byte-neutral wire — the whole point).
- Layer-5 test `ClrJsonActionsArray_WritesOntoStepActionsSlot_AsActionHosts` now GREEN (regressed then fixed).

## RESOLVED — the "3 Data regressions" are pre-existing order-fragility, not this change

Full-suite union wobbled 45→48, but under **controlled (per-class) comparison the change is zero-delta**:
- `ToDictionary_ReturnsAllVariables` + `Set_StripsPercentFromName`: fail the `VariablesTests`-class-only
  run on BOTH baseline AND with-change (2 failed / 58) → pre-existing order-fragile (they only pass in
  the full suite via cross-class ordering luck). NOT caused by this change.
- `Set_GoalStepsBracketIndex_PreservesGoalIdentity`: PASSES the class-only run on both trees.

So all three differ only in full-suite cross-class scheduling; the change perturbs process-static
ordering but introduces no controlled-comparison delta. Wire + Modules: zero delta multi-run. Verdict:
safe to commit increment 1.

Remaining watch-item (not a blocker): step/action now register in `_nameToType`/catalog
(`Registry.cs:203-205`, architect verify #2). Harden the fragile Variables tests OR confirm the
catalog exclusion when `goal` lands and the readers replace reflection.

## Next step

1. Increment 2: `goal` → item (same additive pattern).
2. Then explicit `Write` + `serializer/Reader.cs` per level (replace the delegating Output; flip the
   `.pr` read off the reflection kind); delete the 3 collection classes + re-home methods.
3. Then the singular sweep (names/wire keys/ActionName→Name), migration, acceptance.

Baselines saved: /tmp/data_bl_union.txt (45), /tmp/data_wc_union.txt (48), /tmp/wire_*, /tmp/mod_* .
