# Storage — tagged union, value type

This file goes deep on the C# shape of `app.types.number.@this`: storage layout, construction, parsing, arithmetic, equality, `IBooleanResolvable`. The spine ([../plan.md](../plan.md)) locks the architectural decision; this locks the surface.

## The shape — `sealed class`

`number` is a **`sealed class` named `@this`** — `app.types.number.@this`, immutable, reading like every other type under `app/types/`:

```csharp
namespace app.types.number;

public sealed class @this : System.IEquatable<@this>, global::app.data.IBooleanResolvable
{
    public NumberKind Kind { get; }

    // Tagged union — exactly one slot is meaningful per Kind.
    private readonly long    _i;   // Int, Long
    private readonly decimal _d;   // Decimal
    private readonly double  _f;   // Float (widened), Double

    private @this(NumberKind kind, long i = 0, decimal d = 0m, double f = 0.0)
    {
        Kind = kind; _i = i; _d = d; _f = f;
    }
}

public enum NumberKind { Int, Long, Float, Double, Decimal }
```

A number is still a *value* semantically — immutable (readonly fields, no setters), value equality, no identity, two `5`s are the same — but it lives as a `sealed class` for **codebase consistency**: every other type under `app/types/` is a class, and one struct exception isn't worth its weight this early in the runtime's life. A struct would save heap allocations only in pure-C# arithmetic that never crosses into `Data` (a `list.sum` accumulator), because `Data.Value` is typed `object` (`app/data/this.cs:86`) and boxes any value type the moment it's stored — and that store-into-`Data` path is most of the runtime. The win is C#-internal, unobservable to any PLang app, and a one-keyword flip to capture later if profiling ever flags `number` allocation. Consistency wins now.

It implements `IEquatable<@this>` for value equality and `IBooleanResolvable` because zero (and NaN) is falsy.

`Float` is a *label* (for `ToString`, catalog fidelity, round-trip identity) widened to `double` on entry — single-precision shares the `_f` slot. Standard VM practice: saves a slot and a code path. Re-narrowing to `float` happens at the explicit-OUT cast.

**No `Context`, no `IContext`.** A number has no use for Context after construction. `Resolve(raw, context)` keeps the parameter for factory-signature consistency with other types but never stores it; `From(5)` and the internal factories never see it. A value carrying per-request state that could get parked in the memory stack across requests is an OBP Rule #4 violation — keeping `number` Context-free makes it a genuine value.

### Which CLR kinds collapse into which slots

| CLR type | Storage slot | NumberKind tag (catalog name) | Notes |
|---|---|---|---|
| `sbyte`, `byte`, `short`, `ushort`, `int` | `_i` (long) | `Int` | `byte`/`short` widen to `int` on entry — the LLM-facing catalog already has `byte`/`bytes` reserved for `byte[]` (a different concept), so unsigned narrow-int slots are not user-pickable as PLang types. |
| `uint` | `_i` (long) | `Long` | Doesn't fit `int`'s signed range; promotes one step. |
| `long` | `_i` (long) | `Long` | Native fit. |
| `ulong` | `_d` (decimal) | `Decimal` | Doesn't fit signed `long`; goes through `decimal` (which has range > 2^64) rather than overflowing or losing precision. |
| `Int128` / `UInt128` | `_d` (decimal) | `Decimal` | Past long; decimal's mantissa covers the practical range used in arithmetic. Edge cases past `decimal.MaxValue` fail to `Parse` rather than silently truncating. |
| `float`, `double` | `_f` (double) | `Float` / `Double` | Float widens; Double is native. |
| `decimal` | `_d` (decimal) | `Decimal` | Native fit. |

**`NumberKind` is number's `kind` refinement** — the universal "high-level type + `kind`" model applied to `number`. The PLang `type` is always `number`; the `kind` (`int`/`long`/`decimal`/`double`) is a separate `.pr` field, exactly as `image` is `type=image, kind=jpg`. So `int`/`decimal`/`double`/`long` are **kinds of `number`, not separate top-level PLang types** — `decimal` is to `number` what `jpg` is to `image`. (CLR `int`/`decimal`/`double` remain as storage; `Data<int>` maps to `number` kind=int, `Data<number>` to `number` with the kind decided at runtime by promotion.) `number.Build(value)` sets the kind at build (decimal point → decimal, `e` → double, no point → int/long); the LLM **is** shown these kinds (unlike most types, whose kind is derived silently) because numeric precision is developer-meaningful. A developer who writes `set %x% = 4294967295` (uint max) sees `%x%(number)` kind=long; the LLM never has to think in `uint`. C# at action sites that genuinely takes `uint` casts via `(uint)x` at the boundary.

### Bigger than `decimal` — `BigInteger`, arbitrary precision

Out of scope for this branch's `number`. The `_d` slot is `decimal` (CLR), which carries 28–29 significant digits — enough for 18-digit crypto values with room, not enough for arbitrary-precision integer math (RSA key arithmetic, etc.).

When a real consumer surfaces (a crypto module, a large-int math feature), `BigInteger` slots in as a fifth `NumberKind` (`BigNumber` tag) with its own storage slot:

```csharp
private readonly System.Numerics.BigInteger _big;   // when Kind == BigNumber
public enum NumberKind { Int, Long, Float, Double, Decimal, BigNumber }
```

Promotion table grows one row/column. `Parse` adds a final fall-through for inputs that don't fit `decimal`. Nothing else in the surrounding architecture changes — the umbrella absorbs it.

That work is deferred to a separate branch with a real consumer driving it. Adding `BigNumber` now without a leaf action that opens up the slot is speculative — same out-of-scope discipline as `video`/`audio` in the types vocabulary. Flagged here so future-me knows the umbrella was designed to absorb it.

## Construction

Static factories per kind, plus implicit-IN operators for ergonomics at handler sites:

```csharp
public static @this From(int    v) => new(NumberKind.Int,     i: v);
public static @this From(long   v) => new(NumberKind.Long,    i: v);
public static @this From(decimal v) => new(NumberKind.Decimal, d: v);
public static @this From(float  v) => new(NumberKind.Float,   f: v);
public static @this From(double v) => new(NumberKind.Double,  f: v);

public static implicit operator @this(int v)     => From(v);
public static implicit operator @this(long v)    => From(v);
public static implicit operator @this(decimal v) => From(v);
public static implicit operator @this(float v)   => From(v);
public static implicit operator @this(double v)  => From(v);
```

Implicit IN lets handlers write `Data<number>.Ok(5)` without ceremony — the primitive sits in the matching slot, no re-boxing of the primitive itself.

## Going back out — explicit only

```csharp
public static explicit operator int(@this n)     => n.ToInt32();
public static explicit operator long(@this n)    => n.ToInt64();
public static explicit operator decimal(@this n) => n.ToDecimal();
public static explicit operator double(@this n)  => n.ToDouble();
public static explicit operator float(@this n)   => n.ToSingle();

public int     ToInt32()   { ... }   // throws OverflowException on out-of-range, ArithmeticException on NaN
public long    ToInt64()   { ... }   // throws OverflowException on out-of-range, ArithmeticException on NaN
public decimal ToDecimal() { ... }   // throws on Double NaN/Infinity/out-of-decimal-range
public double  ToDouble()  { ... }   // lossy on Decimal past ~15 digits, never throws
public float   ToSingle()  { ... }   // lossy on Decimal past ~7 digits, never throws (clamps to ±Infinity)
```

Narrowing always **throws on failure** — no silent truncation. The PLang dev who writes `(int)%n%` is asserting "this is an int" — if it's not, that's a typed error at a well-defined boundary, not silent corruption two steps later.

`ToDouble` is the one that never throws because IEEE-754 doesn't have a failure mode for over-range (it saturates to ±Infinity, which is a valid double). Mirrors C#'s behavior on `(double)decimal`.

### Error model — throws at the C# boundary, `Data` at the handler boundary

Two surfaces, one error model where it matters:

- **C# operators and `private` internals throw** — `(int)n`, `a + b`, `decimal`-overflow, integer div-by-zero. This is what every CLR numeric does; in-process C# owns the exception path the way it does for `int`/`decimal`.
- **The module/handler surface always returns `Data`** — never throws to the runtime. `math.add`'s `Run()` returns `Data<number>`; it calls the Data-returning named method `number.Add(a, b, policy)`, which catches `OverflowException` internally and returns `Data<number>.Fail("MathOverflow", …)`. Same shape for the parse/cast boundary: `Resolve` throws, `TryResolve → Data<number>` doesn't; `ToInt32` throws, `TryToInt32 → Data<int>` doesn't.

So an overflow from a strict cast becomes `Data.Error("MathOverflow")` that the Lifecycle/Events `[OnError]` bindings can see — surface-uniform with `Data.Error("ReadFailed")` from file actions. Exceptions never escape a `Run()` boundary; everything in PLang returns `Data`.

## `Parse` — the single string-coercion home

```csharp
public static @this?  Parse(string s);
public static bool    TryParse(string s, out @this? n);
public static @this   Resolve(string raw, actor.context.@this context);   // runtime construction
public static string  Build(object? value);                               // build-time: returns the kind ("int"/"decimal"/…)
```

`Build` is the build-time sibling of `Resolve` — it reads the kind for the `.pr` without constructing the value (decimal point → `"decimal"`, `e`/exponent → `"double"`, no point fitting int → `"int"` else `"long"`). The builder calls it once per literal; the runtime never re-derives.

`Parse` picks the narrowest kind that fits losslessly:

1. **No decimal point, no exponent.** Try `long.TryParse`:
   - Fits in `int` → `Kind = Int`.
   - Otherwise → `Kind = Long`.
   - `long.TryParse` failed (out of int64 range) → fall through to decimal.
2. **Has decimal point, no exponent, no NaN/Infinity sigils.** Try `decimal.TryParse` → `Kind = Decimal`.
3. **Exponent present, NaN/Infinity literal, or decimal parse failed.** Try `double.TryParse` → `Kind = Double`.
4. **All fail.** Return `null` / `false`.

`Resolve(string, context)` is the source-generator-recognized factory — `app.types` catalog reads it (via reflection on the static method) to render `number` as a `scalar` with shape `string`, mirroring how `path` is rendered. Bare numeric literals in `set %x% = 3.14` flow through this entry point at lazy-materialization time, going through the `Serializers` registry. `Resolve` is a thin wrapper around `Parse` that throws if `Parse` returns null (the action-site contract is "this must be a number"). The `context` parameter exists for factory-signature consistency with other types' `Resolve` but is **not stored** — `number` holds no Context (see "The shape").

## Arithmetic

`Add` / `Subtract` / `Multiply` / `Modulo` follow one shape; `Divide` and `Power` carry their own result-kind rule (below). All take a `NumberPolicy` and dispatch on the promoted kind:

```csharp
public static @this Add(@this a, @this b, NumberPolicy policy)
{
    var kind = PromoteKind(a.Kind, b.Kind, policy);
    return kind switch
    {
        NumberKind.Int     => AddIntChecked(a.AsInt64(), b.AsInt64(), policy),
        NumberKind.Long    => AddLongChecked(a.AsInt64(), b.AsInt64(), policy),
        NumberKind.Decimal => From(a.AsDecimal() + b.AsDecimal()),
        NumberKind.Double  => From(a.AsDouble() + b.AsDouble()),
        _ => throw new InvalidOperationException()
    };
}

public static @this operator +(@this a, @this b) => Add(a, b, NumberPolicy.Lenient);
public static @this operator -(@this a, @this b) => Subtract(a, b, NumberPolicy.Lenient);
public static @this operator *(@this a, @this b) => Multiply(a, b, NumberPolicy.Lenient);
public static @this operator /(@this a, @this b) => Divide(a, b, NumberPolicy.Lenient);
public static @this operator %(@this a, @this b) => Modulo(a, b, NumberPolicy.Lenient);
```

Operators always use the lenient default. Policy-aware overloads are what math action handlers call. The dual surface (operator + named static) is deliberate — operators read clean in C# test fixtures and at handler sites that don't care; the named overload is what the runtime uses when policy matters.

### Promotion table (Add / Subtract / Multiply / Modulo)

| Left ↓ Right → | Int | Long | Decimal | Double |
|---|---|---|---|---|
| Int | Int | Long | Decimal | Double |
| Long | Long | Long | Decimal | Double |
| Decimal | Decimal | Decimal | Decimal | **policy.Precision** |
| Double | Double | Double | **policy.Precision** | Double |

The single fork is decimal × double. `policy.Precision == Double` (lenient default) promotes to Double; `Decimal` keeps decimal and throws on double NaN/Infinity/out-of-range. Same fork in both directions.

### Divide and Power leave the integer track

`7 / 2` must not be `3`. Integer division is the right tool sometimes, but it's the wrong *default* for a language whose audience isn't thinking in C# types — a developer who writes `7 / 2` means three-and-a-half. So:

- **`Divide`** never resolves to `Int`/`Long`. `Int / Int` and `Long / Long` promote to `Decimal` under the lenient default (`policy.Precision == Double` promotes to `Double` instead, for chains that are already floating). Decimal/double inputs follow the table above. `7 / 2 → 3.5`.
- **`Power`** promotes by exponent: a non-negative integer exponent on an integer base stays integer (`2 ^ 10 → 1024`, overflow per policy); a **negative** exponent leaves the integer track (`2 ^ -1 → 0.5`, Decimal/Double per precision); a **fractional** exponent is always `Double` (IEEE — `2 ^ 0.5`).
- **Truncating division is explicit.** `math.intdiv` (a named action) does `7 intdiv 2 → 3` and `7 mod 2` pairs with it. A developer who wants the integer-floor behavior names it — the way Python made `//` distinct from `/`.

Division by zero:
- Integer / Decimal: `DivideByZeroException` regardless of policy — there is no integer Infinity, and the handler turns it into `Data.Error`.
- Double (or a kind that promoted to Double): IEEE-754 `±Infinity` / `NaN`.

### Integer overflow

`AddIntChecked` / `AddLongChecked` (and the multiply / power variants) use C# `checked` arithmetic:

- `policy.Overflow == Promote` (lenient default): on `OverflowException`, widen to the next kind. `Int` overflow → recompute as `Long`. `Long` overflow → recompute as `Decimal`. `Decimal` overflow → throw (no wider integer kind exists; decimal's range is far larger than long's, so this rarely fires).
- `policy.Overflow == Throw`: any overflow throws immediately, no widening. Int + Int that wouldn't fit in int → `OverflowException`.

(Division by zero is covered under "Divide and Power leave the integer track" above.)

## `IBooleanResolvable`

```csharp
public Task<bool> AsBooleanAsync() => Task.FromResult(Kind switch
{
    NumberKind.Int or NumberKind.Long       => _i != 0,
    NumberKind.Decimal                       => _d != 0m,
    NumberKind.Float or NumberKind.Double    => _f != 0.0 && !double.IsNaN(_f),
    _ => false
});
```

NaN is **falsy** — generalizing "zero is false" to "not-a-real-number is false." Matches the principle that truthy values are values the dev can compute with; NaN is a poison value that should fail soft (falsy) rather than silently succeed.

## Value equality — lenient default, `ExactEquals` for the careful

**`==` is lenient by default; an explicit `ExactEquals` is there for those who need it.** A regular PLang developer expects `0.1 == 0.1` to be true regardless of whether one side originated as decimal and the other as double — they don't know (and shouldn't have to know) the storage kind. So the default `==` promotes to a common form and compares:

- `number.From(5) == number.From(5L)` → true.
- `number.From(5m) == number.From(5.0)` → true (within double precision).
- `number.From(0.1m + 0.2m) == number.From(0.3m)` → true.
- `number.From(0.1) + number.From(0.2) == number.From(0.3)` → **false** (IEEE-754 — `0.1 + 0.2 = 0.30000000000000004`).
- `number.From(double.NaN) == number.From(double.NaN)` → **false** (IEEE-754 rule).

```csharp
// Lenient — the default. Same Kind → exact slot compare; cross-kind → promote and compare.
public bool Equals(@this other)
{
    if (Kind == other.Kind)
        return Kind switch
        {
            NumberKind.Int or NumberKind.Long    => _i == other._i,
            NumberKind.Decimal                   => _d == other._d,
            NumberKind.Float or NumberKind.Double => _f.Equals(other._f),  // NaN-aware
            _ => false
        };

    var promoted = PromoteKind(Kind, other.Kind, NumberPolicy.Lenient);
    return promoted switch
    {
        NumberKind.Long    => AsInt64() == other.AsInt64(),
        NumberKind.Decimal => DecimalEqualsGuarded(this, other),   // false if a Double operand is NaN/Inf/out-of-range, no throw
        NumberKind.Double  => AsDouble() == other.AsDouble(),
        _ => false
    };
}

public override bool Equals(object? obj) => obj is @this other && Equals(other);
public static bool operator ==(@this a, @this b) => a.Equals(b);
public static bool operator !=(@this a, @this b) => !a.Equals(b);

// Strict — opt-in. Same Kind AND exact slot bits. For crypto / finance / debugging
// where decimal(0.1) and double(0.1) must be distinguished.
public bool ExactEquals(@this other)
    => Kind == other.Kind && Kind switch
    {
        NumberKind.Int or NumberKind.Long    => _i == other._i,
        NumberKind.Decimal                   => _d == other._d,
        NumberKind.Float or NumberKind.Double => _f.Equals(other._f),
        _ => false
    };

// Canonical hash — consistent with lenient ==. Integer-valued numbers across
// Int/Long/Decimal hash the same; non-integer decimals and doubles hash by value.
public override int GetHashCode()
{
    if (TryGetIntegralValue(out long whole)) return whole.GetHashCode();   // 5, 5L, 5m collapse
    if (Kind == NumberKind.Decimal) return _d.GetHashCode();
    return _f.GetHashCode();
}
```

`DecimalEqualsGuarded` is an explicit `IsFinite` + decimal-range check (no `try/catch` for control flow on a hot path). `TryGetIntegralValue` canonicalizes the hash so `From(5)`, `From(5L)`, `From(5m)` share a bucket, keeping the `Equals`/`GetHashCode` contract.

### The honest caveat — lenient `==` is not a clean equivalence relation

Promotion-based cross-kind equality is **not transitive** at the precision boundary. Concretely: `From(0.1)` lenient-equals `From(0.1m)` (both land on the same nearest double), and `From(0.1)` lenient-equals `From(0.1000000000000000055m)` (same nearest double), but `From(0.1m)` does **not** lenient-equal `From(0.1000000000000000055m)` (both Decimal kind → exact `_d` compare → unequal). So `a==b`, `a==c`, `b≠c`.

This is a real hazard for `number` as a key in a `HashSet`/`Dictionary` — hashed collections assume an equivalence relation, and a non-transitive one can collapse distinct values or miss lookups. We accept it knowingly because:

1. **The default is what 99% of developers want** — `0.1 == 0.1` true is the principle of least surprise. The alternative (exact-only by default) surprises everyone to protect a corner.
2. **PLang devs almost never put `number` directly in a C# `HashSet`** — collection membership goes through PLang's list/dict actions, whose equality discipline is the collection layer's, not raw `number.Equals`. The non-transitivity bites only C#-internal code that opts into hashed collections of `number`.
3. **`ExactEquals` is the escape hatch** for code that needs a true equivalence relation (it's reflexive, symmetric, transitive — same Kind, same bits). Crypto/finance/dedup code uses it.

Documented loudly so nobody is surprised; not "fixed," because the fix (exact-only default) is the worse default. NaN-in-a-`HashSet` can never be looked up — same as `double` in C#; we don't paper over IEEE-754.

## `ToString`

`ToString()` formats by Kind, invariant culture:

- `Int` / `Long`: `_i.ToString(CultureInfo.InvariantCulture)`.
- `Decimal`: `_d.ToString("G29", CultureInfo.InvariantCulture)` — round-trip format, preserves trailing zeros only if originally present.
- `Float`: `((float)_f).ToString("R", CultureInfo.InvariantCulture)`.
- `Double`: `_f.ToString("R", CultureInfo.InvariantCulture)`.

`R` gives round-trip-safe formatting for floats / doubles. Culture-aware formatting (commas, currency, ...) lives on a future `number.ToString(culture)` overload that flows through `app.Culture` — out of scope for this branch.

## What stays out of `number`

- **No `Math.Sin` / `Cos` / `Log` / `Exp`** on `number` itself. Those produce doubles, take doubles, and live in `math.*` module actions. `number` carries the value through.
- **No `Round` / `Truncate` / `Floor` / `Ceiling`** on `number` — they're `math.*` actions that take a `number` and return a `number`.
- **No formatting / parsing with culture.** Invariant only; culture handling flows through `app.Culture` separately.
- **No comparison operators (`<`, `>`, `<=`, `>=`)** in this stage. Add when the first comparator handler needs them; for now arithmetic + equality is enough.
