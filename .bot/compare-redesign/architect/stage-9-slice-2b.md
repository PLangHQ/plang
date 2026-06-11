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
- [ ] **Delete `item.ToRaw` at every visibility**, including the leaf-collapse arms (`dict/this.cs`, `list/this.cs`, `type/this.cs:168`, the per-type overrides). Callers route through `Clr` or the type's serializer. This falls out of the site walk — do it when the last caller converts, not before.
- [ ] **`text.Value` → private** (content leaves text only via `Write(IWriter)`/typed ops). `bool`/`binary`/`number` faces follow as their callers dissolve in the site walk — finish text in 2b, take the others as far as the walk carries them, list the remainder for the PLNG003 walk.
- [ ] **Rebuild `Value<T>()` on the ruled mechanics**: `await Value()`, answer-is-T or chain-facet → hand over; else the answer's own Convert hook; else `Data.Error`. Delete `AsT_Impl`, `WrapAs`, `AsCanonical`, and the `_resolvingValues` cycle guard (single-pass render has no recursion to guard).
- [ ] **Delete `Data.RawValue => Peek()`** (`data/this.cs:606`) — a raw-named alias face on the courier.
- [ ] **Tighten the two pins so this can't recur**: `GenericToRaw_DoesNotExist_OnItemBase` asserts no `ToRaw` member at ANY visibility (reflection, `NonPublic` included); `TextRawValue_IsPrivate` likewise asserts private-or-absent, not just non-public.

## You own this

Site-by-site judgment (typed flow vs real edge) is yours — flag any site where neither outcome seems right instead of inventing a third. The rulings above and the two-outcome rule are the contract.
