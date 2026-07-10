# Decision — the reconcile is ASYNC (revises `stage2-compare-reconcile-answer.md`)

**From:** architect. **Settled with Ingi (2026-07-10).** Confirms `coder/stage2-compare-reconcile-should-be-async.md`. The sync ruling contradicted itself — its `data.Compare` eagerly `Value()`'d both sides (for a container: a full deep-render, O(2N), no short-circuit — the shape we rejected for sort) while its own `AreEqual` example was async single-pass. Ingi caught it; the async shape dissolves the contradiction. **Where this doc and the prior one disagree, this one wins.**

## Confirmed shape (the coder's, as written)

- `item.Compare(item)` — **async**, non-virtual, still the two-line reconcile (`Rank` pick + `Invert`; still `other.Order(this)`, never `other.Compare` — the recursion trap stands).
- `protected virtual ValueTask<Comparison> Order(item)` — per-type. Scalars complete synchronously (`new(...)`); **containers walk lazily** — each element pair awaited AS REACHED, first mismatch exits, the deep render never happens.
- `data.Compare` — a thin async door; **no eager `Value()` of the whole graph**. `AreEqual`/`Remove` collapse into this path (thin wrappers or direct `Compare` calls).
- Everything retained from the prior rulings stands: the ×10 Rank table, coercion via the pure `Create` core, `Invert()` + `AsSign()` (already landed), unorderable-in-sort throws with KPR context, the `Compares`/statics/`CoerceOwn` deletions.

## The three confirms, with two flags

1. **[SUPERSEDED by `stage2-compare-materialization-answer.md` — containers stopped deep-rendering, so `data.Compare` shallow-materializes both operands and rank reads off the REAL items; the type-axis rule is deleted.]** ~~Rank pre-materialization: YES — but pick the driver off the TYPE AXIS, never a shallow item's virtual.~~ The trap: a deferred leaf (lazy source, `%var%`) has a shallow item of `source.@this` — its `Rank` would be the base `0`, not the declared type's, and the driver-pick misfires (`%jsonFile% == %x%`). The driver comes from the declared/minted type (type-level, no materialization). **Pin with a deferred-operand test.**
2. **Sort: a small async merge/insertion over the materialized keys** — `Array.Sort`'s sync comparator would force sync-over-async (banned). Over already-materialized keys the compare's awaits complete synchronously, so the async sort is effectively sync in practice, honest in signature. `AsSign()` still throws on unorderable; sort's action boundary still maps it to the KPR error naming both elements.
3. **`@null.Rank` = 1000 (Ingi — round headroom above list 750).** The prior ruling was self-contradictory (Rank 0 + an `Order` that only fires when null drives) — my error, the coder's analysis is right: **the policy owner must drive.** `null == null → Equal`, `null vs value → NotEqual`, both from `@null.Order`, no policy arm in the reconcile. **Stated consequence:** sorting a list containing null THROWS (null drives → NotEqual → no ordering → `AsSign` throws) — consistent with the unorderable ruling, no magic nulls-first ordering; if nulls should ever sort, `@null.Order` is where that answer lives.

## Acceptance (replaces the prior doc's list where they overlap)

- `%listA% == %listB%` with a mismatch at element 0 of 10 000: exits after one element-pair; elements 1..9999 stay unmaterialized (the short-circuit proof, now on the REAL flow).
- The deferred-operand driver-pick test (a lazy source compared against a typed value picks the declared type's rank).
- The templated-element pin (sort and `==` agree — both materialized).
- `sort` over incomparables AND over a null → the KPR error; `==` over the same pairs → not-equal, no error.
