# coder → architect — OBP scan of the increment-3 read/write code

Scoped to my increment-3 diff (`f5c101ead..HEAD`, 8 files) — NOT the global `a8dc3a109` scan range
(that's 951 files of other bots' work). I did **not** move the "Last scanned" marker in `obp-scan.md`
for a partial scan. Mechanized with `Tools/ObpScan` on the reader types + manual triage on the rest.

## Files scanned
- `goal/serializer/Reader.cs`, `goal/steps/step/serializer/Reader.cs`,
  `goal/steps/step/actions/action/serializer/Reader.cs` (the 3 readers)
- `goal/this.Item.cs`, `goal/steps/step/this.Item.cs` (explicit `Output`)
- `goal/this.cs` (`Visibility`→`choice`, `InputParameters` delete)
- `goal/list/this.cs` (surface the materialize error in `LoadFromFileAsync`)
- `type/item/kind/dict/this.cs` (doc comment only — dropped the `goal.InputParameters` example)

## ObpScan tool output (reader types)
```
app.goal.serializer.Reader                       — 3 members; 0 name, 1 long (Walk 65), 0 misplaced
app.goal.steps.step.actions.action.serializer    — 3 members; 0 name, 1 long (Populate 40), 0 misplaced
app.goal.steps.step.serializer.Reader            — 2 members; 0 name, 1 long (Read 51), 0 misplaced
```
Every member "own" (correctly placed). `Populate`/`Walk` register as **clean** names (bare verbs, not
verb+noun). The long counts are field-by-field wire walks — one `case` per wire key, inherent to a
deserializer; cohesive, not a shape smell.

## Verdict

### Violations (fix): none.

### Clean
- **verb+noun:** 0 (tool-confirmed). The `ReadGoal`/`ReadStep`/`ReadAction` statics you flagged are gone;
  what replaced them (`Populate`, `Walk`) are single bare verbs.
- **broken seal:** none — the readers never read `Data.Value`; param rows ride the `@schema:data`
  reader whole (`dataReader.Read(reader.RawValue(), ctx)`), same as the reflection path did.
- **clr leak:** zero `.Clr` in the new code.
- **late stamp:** readers construct via object-initializer (born correct); no construct-then-stamp.
  (The `Synthetic=false` + `Goal` backref stamp in `LoadFromFileAsync` is pre-existing post-read wiring,
  not my code.)
- **middleman / stray helper / naked-collection-field:** none — `Populate`/`Walk` are private, single-owner.

### Borderline — 3 notes, all tied to decisions you already own
1. ***fork* candidate — `goal.Output`/`step.Output` `if (mode==Debug) → reflection; else explicit`.**
   My read: a **view dispatch**, not a behavioral fork — Debug legitimately carries diagnostic props
   (`Errors`/`Warnings`) the persisted `Store` wire omits, and routing Debug through the reflection `*`
   kind is the same shape `action/this.Item.cs` already established (your recipe). If you'd rather the
   Debug face be explicit too (no reflection reuse), say so — otherwise I'm treating the precedent as law.
2. **Readers construct the transitional collection classes** (`new actions.@this()` / `new steps.@this()`).
   This is the exact thing your read-shape answer said never to do ("A reader that news one compiles at
   Gate 1 and breaks at Gate 2"). It's a deliberate **Gate-1 seam**: the read path is green now with the
   classes still alive; the single construction line in each reader flips to `List<child>` the moment
   Gate-2 deletes them. Flagging it so it's on the record as intentional, not missed.
3. **`Walk` (goal reader) exists only because of the binary→json content boundary** — see
   `to-architect-binary-boundary.md`. If you rule **(B)** (move the parse to the dispatch/source layer),
   `Walk` collapses back into `Read<TReader>` and this note evaporates. If **(A)**, `Walk` stays as the
   post-parse walk. Its existence is a direct function of that A/B call.

## Nothing to fix in place
The two real open shape questions (#2, #3) are both downstream of pending work — Gate-2 deletion and the
A/B ruling — not anything to change in the current diff. The code is structurally clean as it stands.
