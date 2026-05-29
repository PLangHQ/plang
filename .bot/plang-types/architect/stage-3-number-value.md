# Stage 3: `number` — the value type

**Goal:** Ship `number` as an immutable `sealed class` value with tagged-union storage, the `kind` derivation, lenient/exact equality, truthiness, and its uniform serializer — the first new proving instance.
**Scope.** The value type and its build/runtime identity. *Included:* `app/types/number/` — storage, `Resolve`/`Parse`, `Build`, operators, equality, `IBooleanResolvable`, `serializer/Default.cs`, `[PlangType]`. *Excluded:* arithmetic *policy* and the `math.*` retype (Stage 4 — operators here are policy-free lenient), comparison operators (`<`/`>` — add when a comparator handler needs them).
**Deliverables (per [plan/storage.md](plan/storage.md)):**
- `this.cs` — `sealed class @this : IEquatable<@this>, IBooleanResolvable`, immutable (readonly slots `_i`/`_d`/`_f`, no setters), `NumberKind { Int, Long, Float, Double, Decimal }`, `Kind`, `static From(int|long|decimal|float|double)`, implicit-IN operators, explicit-OUT casts (throw on lossy narrowing). No `Context`, no `IContext`.
- `this.Parse.cs` — `Parse` / `TryParse` / `Resolve(string, context)` (narrowest-fit; `context` taken for signature uniformity, **not stored**).
- `this.Build.cs` — `static string Build(object? value)` → the kind ("decimal" on a decimal point, "double" on `e`/exponent, else "int"/"long" by fit). The literal-shape rule lives here.
- `this.Operators.cs` — `+ - * / %` and `== !=`, all lenient default (delegate to the policy-free path; Stage 4 adds the policy-aware named methods).
- `this.Equality.cs` — lenient `Equals(@this)` (cross-kind promote-and-compare, `DecimalEqualsGuarded` not try/catch), `ExactEquals(@this)` (same Kind + exact bits), canonical `GetHashCode` (integer-valued kinds share a bucket).
- `serializer/Default.cs` — `(number, *)` → the matching `IWriter` numeric primitive by `Kind` (`Int`/`Long`/`Decimal`/`Float`/`Double`). Uniform across formats.
- `[PlangType("number")]`; the LLM catalog shows number's kinds (int/decimal/double/long) — this is the one type that advertises its kinds.
**Dependencies:** Stage 1 (registry, `Build`, kind field), Stage 2 (serializer dispatch).

## Design

> **You own the code.** [plan/storage.md](plan/storage.md) is the detailed surface; it is design intent, not literal dictation. Final shapes are yours.

`number` is a *value* (immutable, value equality, no identity) living as a `sealed class` for codebase consistency — every other `app/types/` entry is a class, and the struct's only win (stack allocation in pure-C# arithmetic) mostly boxes away when stored in `Data.Value` (which is `object`). Don't reach for `struct` chasing an allocation win that won't materialize; if profiling later flags `number` allocation it's a one-keyword flip.

**`int`/`decimal`/`double`/`long` are number's *kinds*, not separate top-level types** — `decimal` is to `number` what `jpg` is to `image`. The PLang `type` stamped is always `number`; `this.Build.cs` sets the `kind`. `Data<int>` (CLR) maps to `number` kind=int; `Data<number>` leaves the kind for runtime.

**Equality is lenient by default, `ExactEquals` opt-in.** `0.1 == 0.1` is true regardless of storage kind — the default a non-programmer expects. The honest caveat (cross-kind lenient `==` isn't transitive at the precision boundary) is documented in storage.md; it's a knowing trade, not a bug — `ExactEquals` is the escape hatch for crypto/finance. NaN is falsy (`IBooleanResolvable`).

**Error model (matters for the operators ↔ Stage 4 handoff):** C# operators and private internals **throw** (like any CLR numeric); the *handler* surface returns `Data` (Stage 4's `number.Add` catches and returns `Data.Fail`). Operators here are the lenient throwing path the named policy-aware methods will wrap.

CLR-kind→slot mapping (uint→long, ulong/Int128→decimal, etc.) and the `BigInteger`-as-future-fifth-kind note are in storage.md — don't add `BigInteger` now.
