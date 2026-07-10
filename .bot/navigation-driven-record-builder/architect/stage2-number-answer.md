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
- **`IntegerLadder` → `Ladder`** — there is only ONE ladder (fractional kinds don't climb; the mix policy handles them). One thing needs no qualifier; that it covers the integer track is a doc-comment fact, not a name fact.
- **`NarrowInteger` → `Narrow`** — verb+noun, the flashing sign. One verb, the caller's intent. `NarrowStrict` aligns in the same pass (overload or a second verb — coder proposes).
- **`Rung` → `Level`** — "rung" is ordinary English (a ladder's bar) but low-frequency: it fails the transparent-to-a-non-native rule. `Level` is the word Ingi reached for unprompted ("the next level in the ladder"). `Step` is banned — the domain collision we renamed `Descend` away from.
- **`Fits` moves ONTO the Level** — the name is right (one verb, caller's intent), the placement is the stray-helper smell at its smallest: `Fits(in Rung r, v)` reaches into the rung; the level owns its own question: `level.Fits(v)`.
- **Statics, settled precisely** (extends the factory sanction by one clause): *factories on the created type, their binding thunk, and private immutable data tables + private methods inside the owning type are sanctioned; hubs and helper classes stay banned.* `Ladder` (the array) = universal immutable constants — static data is its honest lifecycle. `Narrow` = a **private factory** (`Narrow(bigValue, floor) → @this` constructs a number that doesn't exist yet — no `this` to hang it on). **Never justify a static by performance**: methods cost zero per-instance memory (only fields do), and the static-vs-instance call difference is one hidden argument the JIT inlines — negligible, unmeasurable. Factory-ness justifies statics; perf never does.
- **Number kinds: context-free, verb `Create`, no registry — see `architect/stage2-number-context-free-answer.md` (supersedes this doc's earlier per-App premise AND the briefly-blessed `Build(double)`/registry/"`From` stays" version).** The short form: kinds are context-free singletons (coder-proven: `number.@this` has no Context, `Write` is the value writing itself, arithmetic is ctx-less); the construction verb is `Create(object)`/`Create(double)` (one verb — `Build` dies with `type.Build`, `FromDouble` and `number.From*` fold into `Create`); **the value carries its kind instance** so ctx-less sites never look anything up; the two remaining lookups (declared name, CLR type) use a private immutable map of the 15 singletons inside number — no public registry, no `Discover`.
- **The climb stays signed-biased — affirmed, don't "fix" it.** Unsigned kinds exist for *source fidelity* (a lib/db hands you `uint` — kept) and *explicit declaration* (`as uint` — the developer asked for the can't-be-negative constraint). The climb never *enters* unsigned uninvited: an overflow result landing in `uint` sets a trap for the next subtraction (`3 - 5` → wrap/throw the user never caused) — the no-magic rule applied to arithmetic. `uint + uint` that fits its floor stays `uint` (inputs' kind honored); only the climb refuses the unsigned track.

## The Ladder, target shape (today's logic, new names, kind-token keys)

```csharp
// number/this.Ladder.cs — a LEVEL owns its range and answers its own question.
// Levels hold the kind INSTANCES (context-free singletons — settled in the context-free answer):
private readonly record struct Level(kind Kind, BigInteger Min, BigInteger Max, bool Unbounded)
{
    public bool Fits(BigInteger v) => Unbounded || (v >= Min && v <= Max);
}

// ONE ladder — no qualifier (fractional kinds don't climb; the mix policy handles them):
private static readonly Level[] Ladder =
{
    new("sbyte", sbyte.MinValue, sbyte.MaxValue, false),
    ...
    new("int",   int.MinValue,   int.MaxValue,   false),
    new("long",  long.MinValue,  long.MaxValue,  false),
    ...
    new("bigint", 0, 0, true),                            // the unbounded top
};

// the climb — compute wide, then find the smallest level that holds the result.
// NO exceptions anywhere: math runs in BigInteger (cannot overflow), placement is comparison.
// A private FACTORY (constructs the result number — no `this` exists yet): the sanctioned static.
private static @this Narrow(BigInteger v, kind floor)
{
    var floorLevel = Ladder[LadderIndex(floor)];
    if (floorLevel.Fits(v)) return FromBigIntegerAs(v, floor);   // fits where it started → stays

    foreach (var k in SignedClimb)                               // int → long → Int128 → BigInteger
    {
        if (MaxMagnitude(k) <= floorMag) continue;               // must be strictly wider
        if (Ladder[LadderIndex(k)].Fits(v)) return FromBigIntegerAs(v, k);
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
