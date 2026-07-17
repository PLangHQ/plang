# coder handoff — graph→items

Branch `goal-graph-singular`. Implementing the items-answer.md direction (graph becomes plang items).

## COMMITTED

- **Increment 1** (`be6bbf294`): `action` + `step` → items (modifier rides free via `: action`).
- **Increment 2** (`3839f4b86`): `goal` → item; 2 Variables tests updated to the item model
  (they asserted the reversed clr<goal> carrier).

All four graph classes (`goal`/`step`/`action`/`modifier`) are now `: item.@this, ICreate`. Faces are
additive + TRANSITIONAL: `Type`/`IsLeaf`; `Output` delegates to the reflection (*) kind (byte-identical
wire); reflect-`Set` (base `Get` already reflects; base `Set` threw). Verified: PLang builds clean;
Wire + Modules zero-delta multi-run; Data zero-delta under controlled per-class comparison; layer-5
write green (`ClrJsonActionsWriteTests`).

## Known clash to resolve during modifier re-homing

`modifier.Order` (int nesting-order property, 3 sites: step Clone, actions mint, modifiers.Sort) now
collides by name with the base `item.Order(@this)` comparison verb → CS0108 hide warning (introduced
when `action` became an item, inc 1). Harmless (base `Compare` binds the verb in base scope), but
rename the property (e.g. `Depth`) when `modifiers.Sort`/`RunAsync` move onto `action`.

Also committed: `modifier` now has its own `Type => "modifier"` face (was inheriting "action").

## Sizing done for the remaining pieces (all dedicated-pass, NOT tail-of-session)

- **goal-read flip (`clr<goal>` → `Data<goal>`)** — 33 sites / ~13 files. NOT compiler-guided: sites do
  `(x as clr<goal>)?.Value`, so returning a goal item makes those casts silently null at RUNTIME (no
  compile error). Must audit each site + lean on test deltas. Entry: `goal/serializer/Reader.cs` (the
  obsolete stub returns `clr<goal>(goal)` via the reflection reader — change to return the goal item)
  + `goal/list/this.cs:372` `LoadFromFileAsync` (`as clr<goal>` → the goal item).
- **explicit token `Output`** — graph items serialize in Store (.pr) AND Debug (callstack/llm
  snapshots), so a hand-written Output must reproduce BOTH modes (Debug = all public props). The
  delegating Output already does this correctly; hand-rolling it is high-risk/low-gain until the
  reader flip forces it. Keep delegating until then.
- **singular sweep** — 99 files; carries via reflection wire automatically (WireName derives from
  property) EXCEPT `ActionName→Name` (wire `action→name`, breaks byte-identical → needs the bootstrap
  `.pr` hand-edit + rebuild).

## NEXT — increment 3: explicit Write + readers (the real new code, architect verify #1)

Replace the delegating `Output` with an explicit token `Write`/`Output` per level, add
`app/goal/**/serializer/Reader.cs` (`ITypeReader`), and flip the `.pr` READ off the reflection kind
onto the per-type readers. Recipe: `Documentation/v0.2/defining-plang-types.md` §3-4.
- Golden: item `Write` output byte-identical to today except renamed keys (the singular sweep).
- Param rows (`Parameters`/`Defaults` = `List<data>`) ride the EXISTING `@schema:data` reader.
- `modifier : action` reader constructs the subtype by declared element type; give `modifier` its own
  `Type => "modifier"` face (today it inherits action's "action").
- Then: delete the 3 collection classes (`steps`/`actions`/`modifiers`) + re-home methods
  (items-answer.md table), then the singular sweep (names/wire keys/`ActionName→Name`), migration, accept.

## Detail from increment 1 (retained)

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
