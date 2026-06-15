# v14 handoff — remove the `clr` class from the codebase

**Branch:** `compare-redesign` (clean, builds green, zero regressions). The
signature-as-layer integration just merged (`622005c9a`) — the Data-in-Data clr
**signature** courier is gone. The next work is removing the `clr` class entirely.

**Read first (living docs — they hold the plan):**
- `.bot/compare-redesign/coder/clr-removal-epic.md` — the six jobs `clr` does + the
  suggested order. **Mostly still accurate; the signature case (a big chunk of job
  #3) is now done.**
- `.bot/compare-redesign/coder/v12/report.md` — its "NEXT WORK" §1 named clr removal
  as the highest-leverage move ("clr is the villain that keeps surfacing"), blessed
  deleting `SetValueDirect`, and listed the clr-courier skip families
  (`Cut3 / StoreView / NestedSignedData* / SignedDataInListLiteral /
  Read_WrappedAsTaskFailure`). **Those families are now RESOLVED** by the
  signature-as-layer work (a Data nesting another Data via clr was the signature
  case) — so that "start at the Data-in-Data courier" item is largely complete; what
  remains is the rest of the clr roles below.
- `.bot/compare-redesign/coder/clr-dissolution-design.md` — the design (five
  overloaded roles, each going to its own home). The settled mental model.
- `.bot/compare-redesign/coder/schema-layer-design.md` — what just shipped (for
  context on how the Data-in-Data courier was removed for signatures).

---

## What `clr` is (one paragraph)

`app/type/item/clr.cs` (76 lines) is "rung 2" of the value model — a transparent
carrier for a CLR object plang has no item type for. It's a smell: it's
**transparent** (no `[Out]` tags → reflected as a property bag at the wire, which
is how it leaked `Context → App → CultureInfo` into the signing graph), and it was
never one concept — five accidents sharing a class. Ingi's decision (2026-06-15):
**delete it.** Every value in a `Data` slot must be a real `item.@this`.

## Current `clr` inventory (production, verified on `compare-redesign` this session)

Construction sites (`new …item.clr(`):

| Site | Role | Disposition |
|---|---|---|
| `data/this.cs:252` (`Lift`) | **#1 unowned-POCO fallback** | POCO → native `dict` on entry; non-item at Lift = hard error |
| `data/this.cs:503` (`StampedForm`) | **#2 stamped raw container** (dict/list with `%ref%`) | narrow to `dict.@this`/`list.@this` (exist) |
| `data/this.cs:548` (`SetValueDirect`) | **#4 courier fallback** | delete — see SetValueDirect below |
| `type/this.cs:452,464,483` (`Judge`) | **#2 declared-label carrier** (value + a declared `{name,kind,strict}` it doesn't own) | give non-derivable types a stored kind slot; move the label to the Data `Type` slot. **The one genuinely entangled piece — touches the signed type slot.** |
| `Wire.cs:278,298` (`ReadBody`) | **#3 nested-Data read courier** | likely DEAD now (production no longer writes a bare Data into a value slot — signature is a real layer; Wrap/Unwrap deleted). Verify, then delete the branch. |

`SetValueDirect` (`data/this.cs:544`, 7 callers): the no-lift bypass. Callers:
`Authored`/`StampEntry` (`this.cs:446,537` — stamp templates in place),
`Wire.WrapAsTyped`/`ReadBody` (`:239,277,298`), clone (`this.cs:1160`). Ingi blessed
deleting it; the template-stamp callers need a non-bypass path (lift the stamped
item normally — lifting an item is identity).

`Lower<T>` (24 sites): retires WITH clr (`Lower<T>(x)` ≡ `(x as item)?.Clr<T>()`
once clr is gone). Don't remove before clr.

## What the signature work already cleared
- The Data-in-Data clr **signature** courier (the big live #3 user) — signatures
  are now a real `signature.@this` layer, not a `clr(innerData)`.
- Dead `Wrap()`/`Unwrap()` clr courier — deleted.
- Providers off the action `item` (commit `8fbc6334d`, earlier) — the ~80% bucket.
- BCL leaves (`Guid`/`DateTime` → real items) — done.
- `archive : item` — the compress courier is a real item (not clr-labeled bytes).

## Suggested order (keeps the tree buildable each step)
1. **Verify Wire.cs:278/298 is dead-on-read** and delete the nested-Data clr
   reconstruction (grep for any production path that writes a bare Data into a
   value slot — there shouldn't be one now). Low risk, removes 2 sites.
2. **Job #1 (Lift POCO → dict, `this.cs:252`)** + the parse-fail throw — independent,
   no signing entanglement. A non-item at Lift becomes a hard producer error.
3. **Job #2 declared-label (`type/this.cs`)** — the real work: give non-derivable
   types a stored kind slot, move the declared label off the carrier onto the Data
   `Type` slot. Entangled with the signed type slot (now simpler — signing is a
   layer, so the inner data's `type` slot is plain). Do after #1/#2.
4. **Delete:** the clr fallback → `SetValueDirect` → `Lower<T>` → the clr class.

## State / numbers (zero regressions to preserve)
Baseline on `compare-redesign`: **Wire 17 · Data 21 · Types 12 · Modules 46 ·
Runtime 54 · Generator 0.** Diff every change against this (the suites carry
pre-existing red; only NEW failures matter — `comm -13 base now`).

## Build / test workflow
- `./dev.sh build` (analyzers off, 1-5s); `./dev.sh full` before commit (analyzers on).
- Per-suite: `PLang.Tests/<Suite>/bin/Debug/net10.0/PLang.Tests.<Suite> --timeout 90s`.
- **Data & Wire suites SEGFAULT at teardown AFTER printing** — read counts from the
  log (`grep -acE '^failed '`), not the exit code.
- Failing names: `grep -aoE '^failed [^ ]+' | sort -u`, then `comm -13 base now`.
- csharp-ls LSP flags TUnit/`Assert`/generated symbols + NSec as errors — NOISE;
  trust `./dev.sh build`.
- Production C# edits via Edit/Write only (console-visible); test edits may be
  shell-batched. Announce mutation tests (temporary source breaks) before doing them.

## Deferred follow-ups from the signature work (in `Documentation/v0.2/todos.md`)
Not clr-removal, but adjacent — surface if a clr change touches them:
- **SettingsStore verify-on-read** — a persisted grant is signed on write but the
  context-less store serializer skips verify on read; rides the SettingsStore OBP
  rewrite. (One permission test skipped pointing here.)
- **archive-as-layer** — compress/hash now operate over a signed inner; a few Wire
  tests skipped. NOTE flagged: `Decompress` currently loses the inner value through
  that path — may be a real async-deserialize-of-a-layer bug, investigate.
- **Full response-side HTTP removal** — request-signing is removed; the response-side
  `!ServiceIdentity` extraction is still adapted-not-removed.

## Working with Ingi (carry this)
Lead high-level (design, not file paths); show problem + proposed fix BEFORE editing
production; he steers closely and refines mid-stream; commit + push per clean unit;
surface design/semantics decisions as explicit questions rather than guessing.
When a big change can't reach green incrementally (like removing a load-bearing
type), preserve work on a wip branch + keep the main branch green, then merge.

One sentence to carry: **`clr` is the last transparent carrier — deleting it (start
with the now-dead Wire read courier, then Lift→dict, then the declared-label slot)
finishes the "every value is a real typed citizen" architecture.**
