# v15 plan — remove `clr`, defer `external` (clone-on-write)

> **CANCELLED 2026-06-21 (Ingi): `clr` stays.** We are NOT removing the `clr`
> class. It stays as the closed host-object carrier (navigate / write-if-setter
> / serialize-`[Out]`; `Peek`→self); engine `%!...%` handles keep riding it and
> are NOT itemized. The Lift fallback stays. Everything below — the class→item
> migration buckets, the Lift-fallback-to-hard-error flip, and the deletes of
> `clr` / `SetValueDirect` / `Lower<T>` / Judge declared-label sites / StampedForm
> — is **off**. Kept for history only. Canonical: `clr-dissolution-design.md`
> DECISION (reversed) + `Documentation/v0.2/todos.md`. The one item that already
> shipped (the two dead `clr` read-couriers removed from `Wire.ReadBody`,
> verified dead) stands — that was genuinely dead code, not part of the removal.

**Read this before `v14/handoff.md`** — it supersedes v14's "delete clr → hard
error" framing with what Ingi and I settled on 2026-06-16. v14 is still correct
on inventory, baseline numbers, and build/test workflow.

## The decision (settled with Ingi, 2026-06-16)
1. **Remove the `clr` class now.** A non-item reaching the Lift value slot is a
   **loud producer error**, not a silent carrier.
2. **The foreign-object carrier returns later as `external`, not `clr`** — `clr`
   hard-codes ".NET" into PLang's runtime-independent vocabulary. Deferred until a
   real host-object need exists. Full spec in `Documentation/v0.2/todos.md`
   2026-06-16 and `clr-dissolution-design.md` DECISION 2026-06-16.
3. **`external` will behave like every PLang value: immutable + rebind, via
   clone-on-write** (read = reflection get; write = clone the real host type +
   reflection-set on the clone + rebind; nested set path-copies). NOT
   mutate-in-place, NOT POCO→dict. This reverses the old "reflect-into-live-object
   — rejected" note (that rejection was about dual-representation drift, which
   clone-on-write avoids).
4. **The courier/declared-label machinery dies and does not come back.**
5. **Invariant: no code branches on `is clr`/`is external`** — uniform door only.

## HARD WORKFLOW RULE (Ingi, 2026-06-16)
**Before converting any class into an item, show Ingi the class + the proposed
item shape and WAIT for validation. One at a time, not a batch.** This is a
per-class semantic decision that is his to make. (Saved to memory.)

## Migration buckets (from the live Lift-fallback probe, 2026-06-16)
Land each as a real item BEFORE deleting the class — Lift will hard-error on them
otherwise. Each class→item conversion gets Ingi's sign-off first.
1. **Engine handles via `%!...%` context variables** (the bulk): `app`/Engine,
   `CallStack`, `Channels`, `Variables`, `Serializers`, `actor.context`. They
   reach Lift through the `computed` factories — `item/computed.cs` `Compute()`
   does `Lift(_factory())`, and the factory returns a live engine object. Either
   each becomes an item, or the factory returns one.
2. **Plain-data result records → trivial items:** `loop` `{itemCount, completed}`
   (`app/module/loop/type/loop.cs`), `builder.warning` `{Action, Message}`.
3. **`condition.Operator`-in-a-value smell:** fix the producer (comparison error
   path, `app/module/condition/Operator.cs`) so a behavior object never lands in
   a Data.
4. **Genuinely foreign / test-only:** anonymous types, `System.Object`/`Uri`,
   `RuntimeAssembly`, test POCOs (`Point`, `Node`, `LocalStatus`). `external` is
   deferred, so these tests get adjusted (real items/dicts) or retired with a
   pointer to the `external` todo.

## Suggested order (green at each commit)
1. ✅ DONE this session: removed the two dead `clr` read-couriers in `Wire.ReadBody`
   (verified dead by mutation probe). Committed.
2. Buckets 2 & 3 (easy items + Operator fix) — small, isolated. **Confirm each class first.**
3. Bucket 1 (engine handles → items) — the substantive step. **Confirm each class first.**
4. Flip Lift fallback (`data/this.cs:252`) to a hard error; mop up bucket 4 tests.
5. Delete: `clr` class · `SetValueDirect` (7 callers) · `Lower<T>` (24 sites) ·
   the Judge declared-label sites (`type/this.cs:452,464,483`) · StampedForm
   (`this.cs:503`). See v14 handoff for the full site list + dispositions.

## Remaining `clr` construction sites (verified 2026-06-16, after Wire cleanup)
- `data/this.cs:252` (Lift fallback) → hard error after bucket migration
- `data/this.cs:503` (StampedForm) → narrow to dict/list
- `data/this.cs:548` (SetValueDirect fallback) → delete with SetValueDirect
- `type/this.cs:452,464,483` (Judge declared-label) → the entangled signed-type-slot piece

## Baseline (true counts, reproduced 2026-06-16; the v14 numbers were inflated by
flaky first-run timing — these are stable on the second run):
**Modules 46 · Types 12 · Wire 17 · Data 20 · Generator 0 · Runtime 54.**
Diff every change against this; only NEW failures (`comm -13`) matter. Data & Wire
suites segfault at teardown AFTER printing — read counts from the log.

## Build/test
`./dev.sh build` (1-5s, analyzers off); per-suite
`PLang.Tests/<Suite>/bin/Debug/net10.0/PLang.Tests.<Suite> --timeout 90s`;
`./dev.sh full` before commit. Production edits via Edit/Write only (console-visible);
announce mutation tests before temporary source breaks.
