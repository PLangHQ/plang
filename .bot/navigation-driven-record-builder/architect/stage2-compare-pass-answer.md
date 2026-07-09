# Decision — the compare pass: instance `a.Compare(b)`, the third hub dies

**From:** architect. **Settled with Ingi (2026-07-09).** Answers `coder/stage2-static-create-vs-instance-and-compare-item.md`. Your lean was right on all three; the discussion added the deletions and the ordering trap.

## 1. `Create` stays a static factory — sanctioned, once

A factory constructs what doesn't exist; there is no instance to own it; the behavior sits ON the created type. The instance face already exists (the entity `Create` delegate, model #6). **The sanction: the only sanctioned statics are the factory on the created type + the thunk that binds it to the entity. Everything else stays banned.** Not re-litigated per type — proceed with the two-overload shape as specified.

## 2. The compare pass — AFTER the relocation, one bounded sweep

Hard dependency: instance `Compare`'s coercion line calls the **pure `Create(item)` core**, which is the relocation's product. Compare-first would coerce through the dying hub and rewrite all 13 types twice. Order inside Stage 2: **relocation sweep → compare pass → hub deletions.**

### The shapes

```csharp
// item/this.cs — comparison is the VALUE's behavior; operands are items BY SIGNATURE:
public virtual int Rank => 0;                                // precedence — see §3
public virtual Comparison Compare(item.@this other) => ...;  // base: identity/equality fallback

// data.Compare(other) — the dispatch INLINES here; CompareValues is DELETED
// (obpv name dies with the method — one caller, two lines, method-holds-own-logic):
var (a, b) = (await Value(), await other.Value());
return a.Rank >= b.Rank
    ? a.Compare(b)
    : b.Compare(a).Invert();          // ← order preservation, see the trap below

// bool/this.cs — a per-type override; coercion via the pure core:
public override Comparison Compare(item.@this other)
{
    var b = other as @this ?? Create(other);
    if (b is null) return Comparison.Incomparable;   // not coercible → no error, rank/type answers
    return Value == b.Value ? Comparison.Equal : Comparison.NotEqual;
}
```

### The ordering trap (named acceptance test)

Today's contract: the higher-ranked type drives, but ordering answers in **caller order**. With instance dispatch, when `b` drives we call `b.Compare(a)` — the answer is from b's side, so ordering results must flip back: **`Comparison.Invert()`** (LessThan↔GreaterThan, Equal/NotEqual/Incomparable unchanged; small method on `Comparison`). Miss it and `%a% < %b%` silently inverts whenever the right operand outranks the left. **Acceptance: an explicit test where the RIGHT operand drives and `<` still means `<`.**

### Dies in this pass

`App.Type.Compares` (the registry — the third behavior-hub, after kind/behavior and the convert hub) · static `Compare(object?, object?)` ×13 · static `CompareRank` hooks (→ virtual `Rank`) · `CoerceOwn` ×6 · `data.CompareValues`.

## 3. Rank — inventory first, then Ingi picks the form

Rank is **precedence** (who drives/coerces), used only relationally — never a result (the result is the `Comparison` object, settled long ago), never on the wire. The open question is its FORM:

- An int Rank = **magic numbers** per type (the exact-literals smell in numeric clothing).
- **Named tiers already exist somewhere in the code** (Ingi) — find them and reuse/align rather than minting new ones. If today's `CompareRank` values cluster into tiers, Rank becomes the named tiers (a tie within a tier = caller order drives, acceptable).
- If the values are genuinely per-type distinct, int stays, each value carrying a naming comment.

**Task: inventory the existing `CompareRank` values + locate the existing named tiers; bring the table back — Ingi rules on the form with real numbers in hand.**

## Acceptance (whole pass)

- `%x% == "true"` / `== 404` stay green through relocation + compare pass.
- The right-operand-drives ordering test (above).
- Grep zero: `Compares`, `CompareValues`, `CoerceOwn`, `CompareRank`.
- Verify no raw (non-item) operand reaches `Compare` at runtime — born-native should guarantee it; test, don't assume.

## Logged for later (not this branch)

The type following the kind merge — the entity instance owning behavior outright (construction included), the way kind instances now do. The entity-`Create` delegate is the first step; the full merge is its own design.
