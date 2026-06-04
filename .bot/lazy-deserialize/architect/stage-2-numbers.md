# Stage 2: Numbers — Way 3 (exact C# type as kind, full scalar tower)

> **Note for coder:** every signature, kind name, and promotion rule below is a **suggestion** that captures architect intent — not a contract. You own the implementation, especially the arithmetic promotion matrix — validate it against C# semantics as you build. Push back if a rule reads wrong.

**Goal:** A number carries its exact C# type, and the `kind` *is* that type. No precision is silently dropped. The full C# scalar tower is supported as kinds, stored without loss, with arithmetic that promotes-then-narrows so nothing wraps.

This replaces the `_i/_d/_f` tagged union and the `float→double` collapse. It lands on top of Stage 1: `number` reads toward its exact kind through the reader registry, and declares its CLR types in the distributed `OwnerOf`.

**Scope:**
- `app/type/number/this.cs` — drop the `_i/_d/_f` union (`:27`), the `NumberKind` enum (`:216`), and the `Kinds` list (`:48`); hold the exact CLR value, derive kind from its type.
- `app/type/number/this.Build.cs` (`:25`) — kind from the exact type; remove `float→double`.
- `app/type/number/this.Convert.cs` (`:40`) — `KindToClr` covers the full tower; arithmetic promote/narrow.
- `app/type/number/serializer/Default.cs` — `Read` parses toward the exact kind (Stage 1 added the file; this fills it out).
- `app/data/this.cs:242` — the runtime stamp that collapses `float→double` goes; kind = the value's CLR type.
- `number`'s CLR-type declaration in the distributed `OwnerOf` (from Stage 1) — list the whole tower.

**Dependencies:** Stage 1 (the reader registry + distributed `OwnerOf`). Independent of Stages 3–5; can land right after 1.

**Out of scope:**
- Lazy materialization (Stage 3) — Stage 2's `number.Read` is called eagerly here, lazily later, same code.
- SIMD / vector types — a `uint4`-style value is a **list of numbers**, not a number kind. Nothing vector ships.

**Deliverables:**

1. **Exact-CLR storage.** A `uint` is held as a `uint`, a `BigInteger` as a `BigInteger`, a `decimal` as a `decimal`. The kind is derived from the value's CLR type — no separate `NumberKind` label to drift. (Boxing is already the baseline since `Data.Value` is `object`; an internal struct optimization is your call, but the model is "exact type, kind derived.")
2. **Full scalar tower as kinds.** `sbyte byte short ushort int uint long ulong`, `Int128 UInt128`, `Half float double`, `decimal`, `BigInteger`. `Kinds` (the advertised vocabulary) lists them; `KindToClr` maps each to its CLR type; the distributed `OwnerOf` declares each CLR type → kind.
3. **`number.Read` parses toward the exact kind.** Given `(number, kind)` and a raw string, parse to that exact CLR type. `(number, biginteger)` of `"9999999999999999999999"` → a `BigInteger`, losslessly.
4. **Arithmetic — promote then narrow** (validate against C# as you go):
   - integers → promote to `BigInteger`, compute, narrow to the result kind.
   - binary floats (`Half`/`float`/`double`) → promote to `double`.
   - `decimal` → stays `decimal`.
   - integer ⊕ binary float → `double` (C#'s rule); integer ⊕ `decimal` → `decimal`.
   - `double` ⊕ `decimal` → **error, requires an explicit cast.** Neither holds the other exactly; C# forbids it without a cast, so does PLang. Don't silently pick one.
   - **Result kind** = the wider of the two operand kinds, widened *further only if the value overflows it*. `int + int` stays `int`; `3000000000u + 2000000000u` lands as `long` (not a silent `uint` wrap). Division producing a fraction → `decimal`/`double` per the operands.
5. **Stamp = exact type.** `app/data/this.cs:242` (and `Build`) stamp the kind from `value.GetType()` across the whole tower — `typeof(float)` → `float`, `typeof(uint)` → `uint`, not `double`/`int`.
6. **`NumberPolicy` stays — repurposed, not inert, not deleted.** `number` already carries a developer-facing `NumberPolicy {Overflow, Precision}` (math.* step params + config cascade via `MathPolicy.Resolve`). Way 3 sets the *defaults*; the policy is the developer's explicit override. Both axes stay live (do **not** gut to an inert shell — an ignored axis is a silent lie):
   - **`Overflow`**: `Promote` (default) = Way 3 — BigInteger carrier → narrow to smallest fitting kind, never wraps (`int+int` over → `long`, `uint+uint` over-range → `long`). `Throw` = strict-width — keep the operand kind, error if the result doesn't fit (this is the "error on overflow" capability a developer can opt into).
   - **`Precision`** (the `double ⊕ decimal` mix): default = **`Error`** (the developer must choose — Way 3's "don't silently pick"). `Double` / `Decimal` = standing choice that resolves the mix. Setting Precision *is* the explicit cast, declared once at config/step scope.
   - **Behavior changes to document:** Precision default flips today's silent-`Double` → `Error` (the `DoublePlusDecimal_Errors` goal test expects it). `Promote` now spans the full tower. `Throw` redefined from "throw on overflow" to "strict-width error." Update the existing math arithmetic C# tests to these Way-3 expectations, documenting each.

## Design

**Why exact-type-as-kind beats the union + label.** The value already *is* its type; a separate `NumberKind` enum beside it is a second copy that can drift (smell #6). Derive the kind from the CLR type and there's one source of truth. The old union existed to bound the storage to three slots; under "hold the exact value" there's no union to bound, and `BigInteger` covers every integer width the fixed types can't.

**Why promote-then-narrow is the *correct* (not just easy) choice.** Raw C# `uint + uint` wraps on overflow silently. Promoting to a carrier that can't lose (`BigInteger` for integers) and narrowing to the smallest kind that *fits without loss* means `3e9u + 2e9u` becomes `5000000000` as a `long` — no surprise wrap. The promotion rules themselves are C#'s own (the proven reference); the only place PLang diverges from raw-C# is refusing the silent overflow and refusing the ambiguous `double⊕decimal`.

**Lazy interplay (looking ahead to Stage 3).** Under lazy, an untouched number off the wire is just its text (`"42"`, `"3.14"`) — lossless, it's a string — carrying a `kind` hint from the source. It materializes to the exact CLR type on first touch via this stage's `number.Read`. So Stage 2's reader is what Stage 3's lazy `Data` calls; build it kind-faithful here and lazy gets exact numbers for free.

**`uint4` is not on this axis.** "More granular" splits two ways: a *finer scalar* (covered end-to-end by the tower, down to `sbyte`/`Half`, up to `BigInteger`) and *more numbers in one value* (a vector — that's a list, a different shape). `uint4` is the second; it's a `list` of numbers, never a number kind.
