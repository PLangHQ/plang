# number kind dissolution — roll-out plan (foundation proven)

**From:** coder. **2026-07-10.** The architect's `stage2-number-answer.md`, in flight. Foundation
landed + proven: `number/kind/this.cs` (base) + `number/kind/int/this.cs` (proof-of-shape).

## Proven so far

- Number kinds are `app.type.kind.@this` subclasses (per the architect's per-App/context premise) at
  `type/number/kind/<k>/this.cs`, auto-scanned into the shared `App.Type.Kind[name|clrType]` door.
- The number-kind base adds the number verbs: `Build(value)` (default `ChangeType` to `ClrForm`),
  `FromDouble(m)`, `Write(number, writer)`, `Read<TReader>(ref reader)`.
- `int` claims `typeof(int)` in the global door — **verified no regression** (Types 22 / Data 35),
  so number kinds coexisting with json/list/dict in the one door is safe.

## Roll-out (mechanical, mapped from the three switches)

1. **14 more kind classes** — `sbyte byte short ushort uint long ulong int128 uint128 biginteger half
   float double decimal`. Each: `Name` + `ClrForm` + `Write` + `Read`. Overrides:
   - **Build override** (ChangeType can't reach): `biginteger` (`BigInteger.Parse` / `AsBigInteger`),
     `int128`, `uint128`, `half`.
   - **Write groups** (from `serializer/Default.cs`): sbyte/byte/short/ushort/int → `Int(ToInt32)`;
     uint/long → `Long(ToInt64)`; ulong → long-or-string; float → `Float`; half/double → `Double`;
     decimal → `Decimal`; int128/uint128/biginteger → `String(ToString)`.
   - **Read** (from `serializer/Reader.cs`): per-kind `From(reader.<token>())`.
   - **FromDouble override**: `half` → `From((Half)m)`, `float` → `From((float)m)`; rest inherit.
2. **`number.Kind`**: `NumberKind` enum → `string` token (context-free, `KindNameForClr(_value.GetType())`).
3. **Wire the switches to the kinds** (resolve `App.Type.Kind[kindName|clrType] as number.kind.@this`
   at the ctx boundaries — construction/serialization):
   - `CoerceToKind(value, k)` → `kind.Build(value)`.
   - `serializer/Default.Write` / `Reader.Read` → `kind.Write` / `kind.Read`.
   - `FromDoubleAsKind(m, k)` → `kind.FromDouble(m)`.
4. **Ladder re-key** (`this.Tower.cs` → `this.Ladder.cs`): `Rung`→`Level` (string `Kind`),
   `IntegerLadder`→`Ladder`, `NarrowInteger`→`Narrow`, `Fits(in Rung,…)`→`Level.Fits`. Logic
   byte-for-byte; keys are name tokens.
5. **Delete**: `NumberKind` enum, `CoerceToKind`, `KindFromName`, `ClrToKind`/`KindToClrType`, the two
   serializer switches, `FromDoubleAsKind`.
6. **Acceptance**: arithmetic suite untouched-green (the re-key invisible); grep-zero on `NumberKind`,
   `CoerceToKind`, `KindFromName`.

## Open detail (decided, note for the architect)

Went with number kinds in the **global** `App.Type.Kind` (their `ClrForm` claiming CLR numerics),
per the architect's per-App/context premise — verified safe. If a separate `number.kind` collection is
preferred (separation from item-navigation kinds), it's a local swap; flag if so.
