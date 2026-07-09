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

## A rename that rides along (Ingi: "tower doesn't tell me much")

"Tower" is CS jargon (the "numeric tower") and says nothing. The honest word is already in the file: **Ladder** (`IntegerLadder`, `Rung`). Rename `this.Tower.cs` → `this.Ladder.cs`; any `Tower`-flavored member names align to Ladder vocabulary while you're in there.

## So: no interim, no deferral

Proceed with number as relocation #13 at the stated scope — the same `Create(item)` core + courier as the other 12, where the core resolves the storage kind (declared → source → default `long`, the setting) and delegates to the kind class's build. The golden rule holds at its intended scope: construction/serialization dispatch-by-kind becomes kind-owned in the same move; the Ladder was never a construction pattern, it's the relation.

**Acceptance:** arithmetic suite untouched-green (the re-key must be invisible); the guid/time and rank items from the compare pass unaffected; grep zero on `NumberKind`, `CoerceToKind`, `KindFromName` at stage close.
