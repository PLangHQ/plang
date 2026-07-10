# For architect — the compare pass: `CompareValues` has sync list/dict callers; where does the reconcile live?

**From:** coder. **2026-07-10.** Blocks the compare pass (`stage2-compare-pass-answer.md`). Traced the
full call flow before implementing; hit a gap the answer doc's `data.Compare` sketch didn't cover.

## The gap

`data.CompareValues` isn't just `data.Compare`'s private helper. It has **5 sync callers inside
`list`/`dict`**, all doing element recursion, all this exact shape:

```csharp
// list.Remove (linear scan, first match), list.OrderOf (List.Sort comparator),
// list.Compare (structural positional), list.AreEqual, dict.AreEqual:
entry.CompareValues(match, entry.Peek(), match.Peek())   // reconcile two already-in-hand Data
```

`data.Compare(other)` is **async** (`await Value()`); these callers are **sync** — they run inside
`List.Sort`'s comparator and `SequenceEqual`-style walks, which cannot await.

The answer doc says *"`CompareValues` is DELETED (obpv name dies with the method)"* — right that the
**name** is verb+noun and must die, but its **role survives**: a reconcile of two materialised values
(null policy + rank-pick + `a.Compare(b)` / `b.Compare(a).Invert()`). That role's OBP home isn't specced,
and it's reached from both the async door (`data.Compare`) and the sync recursion (list/dict).

## Second thing the trace surfaced — a latent correctness bug

The sync callers compare `Peek()`, **not** `Value()`. A pending/templated element (`cache: %off%`, a
source not yet rendered) sorts/compares on its **unrendered** form, while `%a% == %b%` (async
`data.Compare`) compares the rendered value. So `sort` and `==` can disagree today. Whatever replaces
`CompareValues` should compare **materialised** values — which is where the async/sync tension bites.

## Options considered

**(1) Phase-split: materialise all N up front, then sync-sort.**
```csharp
for (int i = 0; i < n; i++) keys[i] = await items[i].Value();   // phase 1: O(N) await
Array.Sort(keys, (a,b) => Reconcile(a,b));                       // phase 2: sync
```
**Ingi rejected — O(2N):** a full extra traversal, and it kills the short-circuit — `AreEqual`/`Remove`
return on the first mismatch/match; pre-materialising all N does work that a lazy walk never would.

**(2) Keep comparing `Peek()` (sync, no await).** Rejected — the correctness bug above; `sort` ≠ `==`.

**(3) Lazy await inside each loop, single pass, short-circuit** (coder's current lean):
- `AreEqual` / `Remove` / `list.Compare` become **async** but stay **single-pass** — `await entry.Value()`
  as the loop reaches each element, short-circuit on first mismatch/match. No wasted pass, no O(2N).
- `List.Sort` is the one genuine hold-out (its comparator is sync-by-construction and re-touches each
  element ~log N times). If `Value()` is **materialise-once** (the model says it is), the re-touches are
  cached `Peek()`s — so a lazy await on first touch, sync thereafter, is correct and not a second pass.
  But `List.Sort` gives no hook to await on first touch; would need either a pre-pass **for sort only**
  or an async sort.

## The two open questions for you

1. **The sync-vs-async reconcile.** Is (3) the intended shape — scan ops go async + single-pass +
   short-circuit, and only `sort` gets special handling? Or is there a cleaner model (e.g. sort always
   over a materialised key projection, accepted as O(N) *because sort touches all N anyway*, distinct
   from the scan ops that must stay lazy)?

2. **Where the reconcile lives** (null policy + rank-pick + `Invert`). It's now pure **item-level** (two
   materialised items, no Data, no Peek). Coder lean:
   - `item.Compare(item other)` — **virtual, per-type**: "I'm the driver; coerce other into me via the
     `Create` core; compare same-kind." (the 13 overrides)
   - the reconcile (picks the driver by `Rank`, applies null policy, `Invert()`s when the right operand
     drives) — a **base non-virtual on `item`** (`a.CompareWith(b)`?), called by `data.Compare` (after its
     awaits) and by the list/dict loops (after their per-element await). Name TBD — must dodge verb+noun.

   Or does the reconcile stay on `data` (it owns the null policy today — *"null policy lives here, above
   every driver"*)? If so, the sync list/dict sites reconcile at the **Data** level, not item — which
   reopens the Peek-vs-Value question, since Data hasn't awaited.

## What's unblocked regardless

`Comparison.Invert()` (extension — enum can't own it), `item.Rank` (base `=> 0`, the 13 `override`s per
`stage2-rank-answer.md`'s ×10 table), and the 13 `Compare` overrides' *coercion body* (`other as @this ??
Create(other)`, replacing `CoerceOwn`→dying-hub) are all shape-clear. Only the reconcile home + the
sync/async materialisation model gate the sweep.
