# Decision — number: neither (A) nor (B). The scope is narrower than you read it.

**From:** architect. **Settled with Ingi (2026-07-09).** Answers `coder/stage2-number-is-a-model-redesign-not-a-relocation.md`. Right to stop — but the answer isn't a model redesign or an interim: the plan's number move was always bounded by a line it didn't state crisply. Stated now:

## The boundary

**What ONE kind owns dissolves into its kind class. What relates kinds stays on the Ladder.**

- **Per-kind (dissolves, this stage):** how to *build* a value of my size (`"42"` → int, the `CoerceToKind` arm), how to *read* and *write* one on the wire (the two 15-arm serializer switches), build-from-double (`FromDoubleAsKind`). Each of the 15 kinds gets its class at `type/number/kind/<k>/this.cs` owning exactly that. `CoerceToKind`, `KindFromName`, `ClrToKind`/`KindToClrType`, the serializer switches, the `NumberKind` enum — all dissolve into the kind classes + the one selection door (each kind declares its `ClrForm`; the collection answers name→kind and clr→kind, same as json/list/dict).
- **Cross-kind (STAYS, out of scope):** the **Ladder** — the ordered list of sizes (sbyte → … → bigint) and the climb rule (`2000000000 + 2000000000` doesn't fit int → result climbs to long). "int overflows to long" is not int's knowledge and not long's — it's knowledge about the ORDER between them, so it lives in the one shared place. Same principled split as the compare pass: `Compare` lives on the value, the rank-dispatch lives on `data` — a relation belongs to the relation's owner. Arithmetic/Equality/Operators/Axes keep their logic untouched.

Your three questions, answered by the boundary:
1. **Precision-edge-only does NOT replace promote/narrow.** The Ladder IS `Overflow.Promote`'s implementation — settings-carried, stays. "Precision = decimal places, edge-only" is the rounding axis, not an arithmetic rewrite.
2. **All 15 storage kinds stay** — each a kind class owning build/read/write. No collapse to long/decimal/bigint.
3. **Arithmetic files are out of scope** — except the one mechanical touch below.

## The one touch arithmetic takes: the re-key (Ingi confirmed)

The `NumberKind` enum dies with the switches, but the Ladder's rungs are labeled with it (`Rung(NumberKind, Min, Max, …)`). The rungs **relabel to the kind tokens** the rest of the system uses (kind `"int"`, kind `"long"`). Same ladder, same climb rule, new labels. Find-replace of the key type, not of behavior.

## Renames + one shape fix that ride the re-key (settled with Ingi, code below)

- **`this.Tower.cs` → `this.Ladder.cs`** — "tower" is CS jargon (the "numeric tower") and says nothing; the honest word is already in the file.
- **`Rung` → `Level`** — "rung" is ordinary English (a ladder's bar) but low-frequency: it fails the transparent-to-a-non-native rule. `Level` is the word Ingi reached for unprompted ("the next level in the ladder"). `Step` is banned — the domain collision we renamed `Descend` away from.
- **`Fits` moves ONTO the Level** — the name is right (one verb, caller's intent), the placement is the stray-helper smell at its smallest: `Fits(in Rung r, v)` reaches into the rung; the level owns its own question: `level.Fits(v)`.
- **The climb stays signed-biased — affirmed, don't "fix" it.** Unsigned kinds exist for *source fidelity* (a lib/db hands you `uint` — kept) and *explicit declaration* (`as uint` — the developer asked for the can't-be-negative constraint). The climb never *enters* unsigned uninvited: an overflow result landing in `uint` sets a trap for the next subtraction (`3 - 5` → wrap/throw the user never caused) — the no-magic rule applied to arithmetic. `uint + uint` that fits its floor stays `uint` (inputs' kind honored); only the climb refuses the unsigned track.

## The Ladder, target shape (today's logic, new names, kind-token keys)

```csharp
// number/this.Ladder.cs — a LEVEL owns its range and answers its own question:
private readonly record struct Level(kind.@this Kind, BigInteger Min, BigInteger Max, bool Unbounded)
{
    public bool Fits(BigInteger v) => Unbounded || (v >= Min && v <= Max);
}

private static readonly Level[] IntegerLadder =
{
    new(/* sbyte */ ..., sbyte.MinValue, sbyte.MaxValue, false),
    ...
    new(/* int   */ ..., int.MinValue,   int.MaxValue,   false),
    new(/* long  */ ..., long.MinValue,  long.MaxValue,  false),
    ...
    new(/* bigint*/ ..., 0, 0, true),                     // the unbounded top
};

// the climb — compute wide, then find the smallest level that holds the result.
// NO exceptions anywhere: math runs in BigInteger (cannot overflow), placement is comparison.
private static @this NarrowInteger(BigInteger v, kind.@this floor)
{
    var floorLevel = IntegerLadder[LadderIndex(floor)];
    if (floorLevel.Fits(v)) return FromBigIntegerAs(v, floor);   // fits where it started → stays

    foreach (var k in SignedClimb)                               // int → long → Int128 → BigInteger
    {
        if (MaxMagnitude(k) <= floorMag) continue;               // must be strictly wider
        if (IntegerLadder[LadderIndex(k)].Fits(v)) return FromBigIntegerAs(v, k);
    }
    return From(v);                                              // BigInteger catch-all
}
```

```
trace:  2000000000 (int) + 2000000000 (int)
        compute in BigInteger  →  4000000000          (unbounded — no overflow possible)
        floorLevel(int).Fits?  →  no
        climb: level(long).Fits? → yes  →  long(4000000000)
```

Only the labels and the two names change — the logic, the signed bias, and the level values are byte-for-byte today's behavior. Acceptance stands: arithmetic suite untouched-green.

## So: no interim, no deferral

Proceed with number as relocation #13 at the stated scope — the same `Create(item)` core + courier as the other 12, where the core resolves the storage kind (declared → source → default `long`, the setting) and delegates to the kind class's build. The golden rule holds at its intended scope: construction/serialization dispatch-by-kind becomes kind-owned in the same move; the Ladder was never a construction pattern, it's the relation.

**Acceptance:** arithmetic suite untouched-green (the re-key must be invisible); the guid/time and rank items from the compare pass unaffected; grep zero on `NumberKind`, `CoerceToKind`, `KindFromName` at stage close.
