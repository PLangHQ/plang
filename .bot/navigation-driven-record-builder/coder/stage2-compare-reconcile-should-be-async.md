# For architect — the compare reconcile should be ASYNC, not sync (revises `stage2-compare-reconcile-answer.md`)

**From:** coder. **Settled direction with Ingi (2026-07-10).** The sync `item.Compare`/`Order` split
just ruled works for scalars but breaks on containers — Ingi caught it. This proposes async, which
dissolves the break.

## The break in the sync ruling

The ruling's uniform door is `data.Compare = (await Value()).Compare(await other.Value())`, with `Order`
**sync**. For a container that is O(2N) **and** loses short-circuit:

```
%listA% == %listB%:
   await listA.Value()   → deep-renders ALL N elements   (pass 1, O(N), allocates a rendered list)
   .Compare → listA.Order(listB)  → walks ALL N to compare (pass 2, O(N))
```

`list.Value()` deep-renders every element by design (door recursion, `list/this.cs:589`). So the sync
`Order` can only run *after* a full eager render — the exact O(2N)-with-no-early-exit shape Ingi rejected
for sort. The ruling's own `AreEqual` example is async single-pass precisely because the sync path can't
do it; but the reconcile dispatch (`data.Compare → item.Compare → Order`) is where `%listA% == %listB%`
actually flows, and that's the sync path. The two collide.

## The fix — async reconcile; materialize at the last moment

Make `Compare`/`Order` async. Then each level materializes only what it needs, when it needs it, and
short-circuit is free. Nobody up-stack pre-materializes.

```csharp
// ── item/this.cs ──
public virtual int Rank => 0;                          // sync — TYPE-level, never reads a value

// the reconcile (non-virtual). Rank picks the driver with no materialization; Order awaits leaves.
public async ValueTask<Comparison> Compare(item.@this other)
    => Rank >= other.Rank ? await Order(other) : (await other.Order(this)).Invert();
    // other.Order(this), NOT other.Compare(this) — the non-virtual/virtual split still prevents recursion.

protected virtual ValueTask<Comparison> Order(item.@this other)   // base
    => new(ReferenceEquals(this, other) ? Comparison.Equal : Comparison.NotEqual);

// ── a scalar (bool) — awaits its operand only now, via the pure Create core ──
protected override ValueTask<Comparison> Order(item.@this other)
{
    var b = other as @this ?? Create(other);
    return new(b is null ? Comparison.Incomparable
             : Value == b.Value ? Comparison.Equal : Comparison.NotEqual);
}

// ── a container (list) — walks elements, awaits each pair AS REACHED, exits on first mismatch ──
protected override async ValueTask<Comparison> Order(item.@this other)
{
    if (other is not @this lb) return Comparison.Incomparable;
    int shared = System.Math.Min(Count, lb.Count);
    for (int i = 0; i < shared; i++)
    {
        var c = await Items[i].Compare(lb.Items[i]);          // lazy: element i renders now, not before
        if (c is Comparison.Less or Comparison.Greater) return c;
        if (c is Comparison.NotEqual or Comparison.Incomparable) return Comparison.NotEqual;
    }
    return Count.CompareTo(lb.Count) switch { <0 => Comparison.Less, >0 => Comparison.Greater, _ => Comparison.Equal };
}

// ── data/this.cs — the door is now a thin await; no eager Value() of the whole graph ──
public ValueTask<Comparison> Compare(@this other) => /* shallow item */ .Compare(/* other shallow item */);
```

No eager `Value()`, no O(2N), no Peek-vs-Value bug — the deep render never happens; each element renders
exactly when compared, never past the first mismatch. `AreEqual`/`Remove` collapse into this same path
(they become thin wrappers over `Compare`, or callers just use `Compare` directly).

## Two things to nail

1. **Rank without materialization.** `Rank` is type-level (`list` is always 750), so `data.Compare` reads
   it off the **shallow** item (Peek/structure) or the type — never by deep-rendering. This keeps the
   driver-pick cheap and is what lets the container Order stay lazy. Confirm Rank is legitimately readable
   pre-materialization (I believe yes — it's a constant per type, the current `type.Rank(other)` already
   reads Type without touching the value).

2. **`Array.Sort` wants a SYNC comparator** — the one place async doesn't drop in. Sort touches all N by
   definition, so it materializes keys up front anyway (the accepted single pass, not the rejected one).
   Over already-materialized keys a compare's awaits complete synchronously. Cleanest: a small
   async-capable sort (merge/insertion over the N materialized keys) rather than `Array.Sort` +
   sync-over-async (banned). `AsSign()` still throws on unorderable; sort's action boundary still maps it
   to the KPR error. Confirm the async-sort approach (vs. keeping a sync compare core just for sort).

## What stays from the prior rulings

- `Rank` ×10 table + `@null` owning null policy via `Rank`/`Order` (see note below), coercion via the pure
  `Create` core, the `Compares` registry + static `Compare` ×13 + `CompareRank` statics + `CoerceOwn`
  deletions, `Invert()` order-preservation with its named test, unorderable-in-sort throws.
- `Invert()` + `AsSign()` land as `Comparison` extensions regardless (already written).

## One more, orthogonal to sync/async — `@null`'s rank

The ruling gives `@null` `Rank => 0` ("never drives") **but** its `Order` encodes "null vs value → NotEqual",
which only fires if null **drives**. With `Rank => 0`, `value.Order(null)` runs instead and returns
`Incomparable` (the Create core declines null) — breaking `%x% == null`. For `@null`'s own `Order` to hold
the policy in both directions it must **drive**, i.e. **highest rank**. Proposing `@null.Rank` = above
`list` (e.g. 800). Confirm — else null policy needs an explicit arm back in the reconcile.
