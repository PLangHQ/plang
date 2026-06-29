# Stage 4 — finish context-never-null for reads

**Design authority:** `plan.md` "Phase 4". Firmed up after Stage 3 green + pushed (`96cfb4c20`).

## Entry
- ✅ Stage 3 green + pushed.

## ⚠️ Known coupling — WireLocal removal regresses "the 15"
A prior attempt to delete `WireLocal` was **reverted: it regressed 15 tests** (commit `0ad8e9083`).
Cause: `WireLocal` is the **context-less, `sign:false`** default `[JsonConverter]` on Data. Delete it
and a bare `Deserialize<Data>` falls to the channel's **signing** Wire, which **activates verify-on-read**
— and a fixture/context-less read has no actor to verify against → fail. So the `WireLocal` deletion
is **coupled to Stage 5** (born-with-context fixtures + the 15). Do the safe flips here; gate the
deletion on the Stage-5 fixture sweep (or do 4+5 as one push).

## Live targets (verified)
- `WireLocal` class: `data/WireLocal.cs` (`: Wire`, `base(View.Store, sign:false)`).
- `[JsonConverter(typeof(WireLocal))]` on Data (`this.cs:24`) and `Data<T>` (`this.cs:920`).
- `Wire._context!` null-forgiving at `Wire.cs:213` (the ReadContext build). No separate `_context==null`
  fail-closed branch remains (already gone) — just the `!` guard.
- `Data._context = null!` field init (`this.cs:36`); no-context fallbacks: ctor `else …Judge` arm
  (`this.cs:201` guard `else if (_context != null)`), `Declare` (`:245`), guards at `:501`, `:721`.
- `source` context-less arm: `source.Read` **branch 2** (`source.cs:126-127`, `_value is string → Convert`)
  + `source._context` nullable (`source.cs:30-31`).
- Data reads that MUST be context-ful before deletion: `item/serializer/json.cs:101,171` (NestedOptions ✅),
  `http/code/Default.cs:513,562,834` (`_transportInOptions` — **verify it carries a context-ful Wire**),
  plus any IMPLICIT nested-Data in an STJ tree whose options lack a Wire.

## Exit
- `WireLocal` + both `[JsonConverter]` deleted; every Data read goes through a context-ful Wire
  (`Wire.ReadOptions`) so verify-on-read has an actor.
- `Wire._context`, `Data._context`, `source._context` structurally non-null; the no-context
  `Judge`/`Convert` fallback arms (ctor else-Judge, `Declare` else-Judge, `source.Read` branch 2) deleted.
- The `signature` reader verifies with the actor in scope. Build + both suites green; the 15 pass
  because the read is correct (Stage-5 fixtures born-with-context), not because a branch was silenced.

## Mechanism (why removal regresses 15) — from the prior trace (todos.md 2026-06-27)
`WireLocal` is context-less, so it hit `Wire`'s `if (_context == null)` "trust-at-rest / fail-closed"
branch and **SKIPPED signature verify**. A context-ful `Wire` **RUNS verify on every nested
`@schema:data`**. So once reads become context-ful, reconstructing an inner signed Data verifies where
it never used to → 15 fail (baseline 21 → 36 on the naive attempt). Other surfaced bits: a serialize-side
`clr.Navigate` NRE on a context-less clr, and `TypeOwnedReadParityTests` output diffs.

## THE load-bearing decision (locked) — nested reconstruction skips verify
An inner Data is **already covered by the OUTER signature**. So reconstructing a nested `@schema:data`
must **NOT re-verify** — a **no-verify flag** on the Wire used for nested reconstruction. The OUTER read
(a transport message, http inbound) DOES verify; inner layers don't. This is the decision the naive
attempt lacked.

## Plan of attack (focused unit)
1. **No-verify flag** on `Wire` (and threaded to the `signature` reader) for nested reconstruction;
   `json.NestedOptions` + the goal-param reconstruction use it. The OUTER transport read keeps verify.
2. **Context-ful the holdouts:** `http _transportInOptions` → Store view + `Transport.ForInbound` +
   context-ful Wire (NOT `InboundOptions` — that's Out view; the view mismatch was a regression).
   `Wire.ReadBody`'s 3 context-less Data births → `context: _context`.
3. **Flip + delete:** `Wire._context` non-null, delete the `_context==null` fail-closed block + the
   parameterless `Wire()` ctor; delete `WireLocal` + both `[JsonConverter]`; delete the ctor/`Declare`
   `else Judge` arms + `source.Read` branch 2; flip `Data._context`/`source._context` non-null.
4. **Fixture sweep (Stage 5):** the residual fixture failures = born-with-context, so the 15 pass on a
   correct read.

> The prior trace's own note: "Do it as a focused unit (not at the tail of a long session)." The
> nested-verify decision (#1) is the part the revert was missing.

## Shipped (prerequisites landed, green) + what's left
**Landed + committed (all green, the WireLocal deletion's prerequisites):**
- ✅ **Build parity** — `type.Build` now handles the Variable write-target (`%s%`) + `%ref%` template
  defer, like `Judge` (additive; the ctor fork stays until Judge can die).
- ✅ **No-verify-for-nested** — `ReadContext.Verify` + `Wire(verify)` threaded to the `signature` reader;
  nested reconstruction (NestedOptions, GoalReadOptions, goal.call/actions readers) sets `Verify:false`.
  This is the decision the prior revert lacked.
- ✅ **http inbound context-ful** — `TransportIn(context)` (copy `[In]` + a context-ful Wire); the last
  explicit context-less Data read is gone.
- ✅ **`%ref%` regex deduped** to `text.@this.HasHoles(string)` (one home).

**What's left — the WireLocal deletion = Stage 5.** Deleting `WireLocal` + the 2 `[JsonConverter]`
attrs compiles clean and regresses **exactly the 15** (Defaults/FilePaths/ResolveValue/RunGoalAsync/
StartGoal). The no-verify flag held (it's not the nested-verify cause). The 15 fail on **varied** causes:
- the strict data reader throws `value slot has no declared type` on an untyped `%ref%` slot (`%!data%`)
  that WireLocal tolerated → data-reader needs leniency: an untyped `%ref%` → text;
- `[CreateVariableDeclined] %Name%` / `[CreateItemDeclined] %path%` → `%ref%`/path values reaching
  `Create` instead of being deferred/resolved (a Build/resolve-path gap beyond the ctor `Build`).
Reverted the deletion to keep the branch green; the deletion + these 15 are the focused Stage-5 debug.
