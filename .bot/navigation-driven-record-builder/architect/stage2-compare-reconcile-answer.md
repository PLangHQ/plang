# Decision — the reconcile lives on `item`; scan ops go lazy, sort materializes; unorderable throws

**From:** architect. **Settled with Ingi (2026-07-10).** Answers `coder/stage2-compare-sync-callers-and-reconcile-home.md`. Your trace was right on both counts: `CompareValues`' *role* survives its name, and the Peek-vs-Value disagreement (sort ≠ `==` today) is a real latent bug — the ruling fixes it by construction.

## Q1 — the operation's access pattern decides

- **Short-circuit ops** (`AreEqual`, `Remove`, positional compare): **async, single-pass** — `await entry.Compare(match)` (the existing data door), awaiting each element only as the loop reaches it. First mismatch/match exits. No pre-materialization, no new machinery.
- **Sort touches all N by definition** — there is no short-circuit to kill, so materializing each row **on the way in** (one pass, first-touch, cached by materialize-once) is work sort was always going to force — moved in front of the sync wall, not added. Then the comparator runs sync over cached values. This is NOT the rejected O(2N) (that version pre-materialized for the short-circuiting ops too).

One rule: *lazy where the op can exit early; materialize up front where the op must see everything anyway.* Every path now compares **materialized** values — sort and `==` agree by construction.

## Q2 — the reconcile is `item.Compare`; the per-type half is `Order`; `@null` owns its own policy

```csharp
// ── item/this.cs ──────────────────────────────────────────────────────────────
public virtual int Rank => 0;                        // the ×10 table (stage2-rank-answer.md)

public Comparison Compare(item.@this other)           // NON-virtual — the reconcile, two lines
    => Rank >= other.Rank
        ? Order(other)                                // I drive; caller order preserved
        : other.Order(this).Invert();                 // other drives → flip the ordering back
        // NOTE: other.Order(this), NOT other.Compare(this) — Compare would re-run the
        // rank pick and recurse. The non-virtual/virtual split does real work here.

protected virtual Comparison Order(item.@this other)  // the per-type hook — 13 overrides
    => ReferenceEquals(this, other) ? Comparison.Equal : Comparison.NotEqual;
```

```csharp
// ── bool/this.cs — a type's Order: coerce via the Create core, compare same-kind ──
protected override Comparison Order(item.@this other)
{
    var b = other as @this ?? Create(other);          // the PURE CORE — null = not coercible
    if (b is null) return Comparison.Incomparable;    // not an error: "abc" == true → not equal
    return Value == b.Value ? Comparison.Equal : Comparison.NotEqual;
}

// ── null/this.cs — the null CITIZEN owns null policy; zero policy arms in the reconcile ──
public override int Rank => 0;                        // lowest — never drives against a real value
protected override Comparison Order(item.@this other)
    => other is @this ? Comparison.Equal : Comparison.NotEqual;   // null==null; null vs value → not equal
```

```csharp
// ── data/this.cs — the async door: await both, then the item reconcile ──
public async ValueTask<Comparison> Compare(@this other)
    => (await Value()).Compare(await other.Value());
```

```csharp
// ── list: a short-circuit op (async, single-pass) ──
public async ValueTask<bool> AreEqual(@this other)
{
    if (Count != other.Count) return false;
    for (int i = 0; i < Count; i++)
        if (await Items[i].Compare(other.Items[i]) is not Comparison.Equal) return false;
    return true;
}

// ── list: SORT — materialize on the way in, sync comparator over cached values ──
// RENAME: list.OrderOf → list.Sort (Ingi — OrderOf was a verb+preposition smell on its own,
// and it collided with the Order hook; Sort is the caller's exact intent).
public async ValueTask<@this> Sort(...)
{
    var keys = new item.@this[Count];
    for (int i = 0; i < Count; i++) keys[i] = await Items[i].Value();   // first-touch; cached
    System.Array.Sort(indexes, (x, y) => keys[x].Compare(keys[y]).AsSign());
    ...
}
```

```csharp
// ── Comparison's two companions (extensions — an enum can't own methods) ──
public static Comparison Invert(this Comparison c) => c switch
{
    Comparison.LessThan    => Comparison.GreaterThan,
    Comparison.GreaterThan => Comparison.LessThan,
    _ => c,                                           // Equal / NotEqual / Incomparable unchanged
};

public static int AsSign(this Comparison c) => c switch   // the .NET sort-convention exit (As-family,
{                                                          // like number.AsBigInteger/AsDouble)
    Comparison.LessThan    => -1,
    Comparison.Equal       =>  0,
    Comparison.GreaterThan => +1,
    _ => throw new InvalidOperationException("no ordering exists between these values"),
};
```

## Unorderable in sort THROWS (Ingi's ruling)

`Incomparable` (and `NotEqual` without an ordering) stays a **value for questions** — `%a% == %b%` answers not-equal, `if` branches, no throw. It becomes an **error only for ops that require a total order**: sort. Silently parking incomparables (stable/0) is a quiet lie; ordering them first/last is invented magic. Precedent: the raw operators already throw on `double ⊕ decimal`. The throw lives in `AsSign` (a sign *does not exist*); sort's action boundary converts it to the error `Data` with KPR-grade context — *"cannot sort %list%: element 3 (dict) is not comparable with element 0 (number)"* — name the elements, not just the fact.

## What this settles / kills

- `CompareValues` — name AND role now have owners: the reconcile = `item.Compare` (base, non-virtual); dispatch callers = `data.Compare` + sort's comparator; scan ops use the data door directly.
- The null-policy arm — dissolved into `@null`'s own `Rank`/`Order` (the "unknown kind = base instance" move again: the citizen owns its policy).
- The Peek-vs-Value sort bug — dead by construction.
- Unchanged from prior rulings: the ×10 Rank table, `Invert` order-preservation with its named acceptance test, coercion via the pure `Create` core, the `Compares` registry + statics + `CoerceOwn` deletions.

## Acceptance additions

- A sort over a list containing a pending/templated element materializes it and agrees with `==` (the latent-bug pin).
- `sort` over incomparables → the KPR error naming both elements; `==` over the same pair → not-equal, no error.
- Short-circuit ops: a test proving early-exit (an element AFTER the mismatch stays unmaterialized).
