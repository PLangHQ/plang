# Stage 9 — slice 2b: finish the consumer tail (reopened 2026-06-11)

**Why:** slice 2 killed the implicit operators by replacing each site with an explicit `.Value` read (~90 prod + ~100 test sites). Sampling shows those sites are mostly NOT .NET edges — they're our own interior code (`condition/Operator.cs:132,140,198`, `cache/wrap.cs:25,27`, `debug/tag.cs:43`), and the recurring pattern `X.Peek() as text.@this)?.Value` is the old `is string` arm with a new spelling. Because that strategy needs an untyped raw door, `item.ToRaw` survived as `internal` (plus the leaf-collapse arms in dict/list/`type/this.cs:168`), and `Value<T>()` shipped as a rename over the old `AsT_Impl` machinery. Three demolition verdicts are therefore unmet; the verdicts are the stage contract, so the stage reopens for this slice. The v7 report flagged all of this honestly (open points 1–5) — this file is the rulings plus the work.

## Rulings on the v7 open points

1. **Scalar equality on an unread reference — your provisional position is CORRECT.** Comparison is a use; uses go through `Value()`; `Value()` resolves/parses. Keep it, un-mark the provisional.
2. **`internal` vs `private` — neither: REMOVE.** The "different edge mechanism" already exists and is blessed: `item.Clr(Type)`/`Clr<T>` — targeted, checked, loud on loss. `ToRaw` is the untargeted version that invites branching on the result; that's why the verdict said remove, not demote.
3. **Async `Write` — accepted as a sequenced prerequisite, NOT part of 2b.** Moving the channel pipeline off the sync STJ converter is its own work item; it stays a tracked stage-9 contract item (listed in the stage file), not silently absorbed. Until then the documented STJ pre-resolve covers templates on that path.
4. **Peek()/Open() tightening toward `item?`** — agreed, revisit after 2b shrinks the raw-shape surface.
5. **`Ready()` → `Value()` naming** — resolves itself once the wrapper faces go private; no separate decision needed.

## The work (each ends green on both suites)

- [ ] **Walk the `.Value`-face sites** (your own `slice2-worklist.md` has the list — reuse it). Per site, exactly two outcomes: **typed flow** (text equality/`Contains`, `bool.IsTruthy`, number ops, awaited `Value()` — ask the item) or **a proven .NET edge** → `item.Clr` (targeted). There is no third outcome; `(Peek() as X.@this)?.Value` is not typed flow.

  **`Clr` is not the new ToRaw — its conditions are strict:** allowed ONLY immediately before a .NET-native call that takes the primitive, AND only after you've verified the item/data does not already provide the operation. If the typed operation is missing, **log it** (keep a running `missing-typed-ops.md` in your folder, one line per case: site, the .NET call, the operation you needed) — the architect reviews the list and we likely add the method to the item instead. An interior `Clr` — one whose result flows into our own code rather than straight into the .NET call — is the same violation with a narrower name.
- [ ] **Delete `item.ToRaw` at every visibility**, including the leaf-collapse arms (`dict/this.cs`, `list/this.cs`, `type/this.cs:168`, the per-type overrides). Callers route through `Clr` or the type's serializer. This falls out of the site walk — do it when the last caller converts, not before.
- [ ] **`text.Value` → DELETED, not private** (Ingi's ruling): no `.Value` property at any visibility — the backing is a private field; content leaves text only via `Write(IWriter)`, the typed ops, or the door (`Value()`). No Peek-cheating either: `Peek()` hands the typed instance, and with no raw face there is nothing to extract from it. `bool`/`binary`/`number` faces head the same way as their callers dissolve in the site walk — finish text in 2b, take the others as far as the walk carries them, list the remainder for the PLNG003 walk.
- [ ] **Rebuild `Value<T>()` on the ruled mechanics**: `await Value()`, answer-is-T or chain-facet → hand over; else the answer's own Convert hook; else `Data.Error`. Delete `AsT_Impl`, `WrapAs`, `AsCanonical`, and the `_resolvingValues` cycle guard (single-pass render has no recursion to guard).
- [ ] **Delete `Data.RawValue => Peek()`** (`data/this.cs:606`) — a raw-named alias face on the courier.
- [ ] **Tighten the two pins so this can't recur**: `GenericToRaw_DoesNotExist_OnItemBase` asserts no `ToRaw` member at ANY visibility (reflection, `NonPublic` included); `TextRawValue` pin asserts text has **no property named `Value` at any visibility** (the backing is a field, private).
- [ ] **`assert.AreEqual` → `data.Compare`** (`assert/code/Default.cs:150-164` and `:246-247`): the unwrap + `Convert.ToDouble` + `ToString()==ToString()` body is a second comparison engine beside THE entry we built (stages 4–5, the numeric tower). Delete the body; `await expected.Compare(actual) == Comparison.Equal`.
- [ ] **`assert` `Contains` dispatcher** (`:196-232`): seven arms; the directory arm's own comment ("the type owns it") is the rule — universalize it. `Contains` becomes a virtual on item (text: substring ordinal-ignore-case; list: its Compare loop moves inside; dict: key membership; directory: already done). The `is IEnumerable` arm and the `ToString()` needle fallback do not migrate — born-typed makes them unreachable/forbidden.
- [ ] **`IsEmpty` becomes an async virtual on item** (`ValueTask<bool>`; default false; null → true, text → whitespace-only, dict/list → count 0; a reference may load to answer — same precedent as `IBooleanResolvable`). `condition/Operator.cs` `IsEmpty` collapses to asking the item; the `is string` and `ICollection` arms are dead by construction.
- [ ] **Delete the `"this"` probe in `variable/set.cs` ValidateBuild entirely** (the unwrap arms AND the check) — Ingi: it existed because the old builder got confused; no longer needed.
- [ ] **The five copy-paste Parse arms** (`v is item.@this { IsLeaf: true } l ? l.ToRaw() : v` in bool/date/datetime/time/duration) — same transform, five sites: one typed source-face seam in the Convert machinery replaces them when ToRaw dies.

## Detection checklist + exit gate (Ingi's three clues)

A site is a violation when it has any of: **(1) `if`/`switch` on plang types** outside the type's own family — each type answers for itself; **(2) a static method receiving the value from outside the type** (private statics on a type's OWN backing are fine — the stateless-behavior rule); **(3) a parameter typed `object` holding a plang value** — decompose-to-object exists only at a proven leaf.

Exit gate for the slice: `grep -rn 'is global::app\.type\..*@this|as global::app\.type\..*@this' PLang/app --include=*.cs` excluding `PLang/app/type/` returns **only proven leaves** (currently 61 production hits; 11 of them in `data/this.cs` itself, which die with the `Value<T>` rebuild). Same sweep for `\.ToRaw\(\)` → zero (member deleted). `CommandLineParser`'s two sites are the documented perimeter (its own standing todo, not 2b).

## Worked examples (the two patterns behind most sites)

**Door confusion — `cache/wrap.cs:24-27`**: `Key?.Peek() as text.@this` then `.Value` for a value the handler USES. Live bug, not style: `Peek` resolves nothing, so an authored template key `"user-%id%"` becomes the literal string `user-%id%` — one shared cache entry for every user. The handler uses the values → `await Key.Value()` (renders), key stays `text`, the cache keys on text (our registry, typed).

**Type ladder — `Operator.cs` `IsEmpty`, `assert` `Contains`/`AreEqual`, `set.cs` unwrap arms**: `is`/`as` arms above the type, usually unwrapping then ALSO handling the raw world (`is text.@this → .Value` followed by `is string`) — two worlds, one hole. Fix is always the same: the question becomes a member on item, each type owns its answer, the dispatcher collapses to one ask.

## The benchmark: `variable.set` collapses toward `put`

`set.cs` is 403 lines; the target is ~2 (`Type != null` → typed ask with the authored entity; then `Context.App.Variable.Put(Name, value)`). Every block has an owner now: the `"this"` probe dies (above); type-entity reconstruction from the wire dict (`TypeFromWire`/`FromName`/canonicalise/context re-stamp) belongs to the **.pr-load entry lift** — `type` is an item (settled), it arrives AS `type.@this`, the handler never sees a wire dict (small named work item: type-entity lift at the entry seam); conversion/coercion belongs to `Value<T>()`/Convert hooks; strict probes ride the typed ask (hooks already type-owned); unwrap arms die in the site walk; the file/url stays-itself case falls out (a courier that opens no doors needs no exemption for not opening them); CopyProperties/OnChange belong to the store's `Put`. **Use set.cs's line count as the debt meter** — when the seams are done it collapses; if a block won't collapse, the block names a seam we missed.

## You own this

Site-by-site judgment (typed flow vs real edge) is yours — flag any site where neither outcome seems right instead of inventing a third. The rulings above, the two-outcome rule, the detection checklist, and the exit gate are the contract.
