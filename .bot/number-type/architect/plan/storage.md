# Storage — tagged union, no boxing

This file goes deep on the C# shape of `app.types.number.@this`: storage layout, construction, parsing, arithmetic, equality, `IBooleanResolvable`. The spine ([../plan.md](../plan.md)) locks the architectural decision; this locks the surface.

## The shape

```csharp
namespace app.types.number;

public sealed class @this : modules.IContext, global::app.data.IBooleanResolvable
{
    public NumberKind Kind { get; init; }

    // Tagged union — exactly one slot is meaningful per Kind.
    private readonly long    _i;   // Int, Long
    private readonly decimal _d;   // Decimal
    private readonly double  _f;   // Float (widened), Double

    [System.Text.Json.Serialization.JsonIgnore]
    public actor.context.@this? Context { get; set; }

    private @this(NumberKind kind, long i = 0, decimal d = 0m, double f = 0.0)
    {
        Kind = kind; _i = i; _d = d; _f = f;
    }
}

public enum NumberKind { Int, Long, Float, Double, Decimal }
```

`Float` is preserved as a *label* (for `ToString`, catalog fidelity, round-trip identity) but widened to `double` on entry — single-precision shares the `_f` slot. Standard practice in most VMs; saves a slot, saves a code path. Re-narrowing to `float` on exit happens at the explicit-OUT cast.

The class is `sealed` (no variants — numeric kinds don't have file-vs-http-style storage divergence). Implements `IContext` because `Data<number>` propagates Context through the runtime and `number.Resolve(string, context)` needs it to look the same as `path.Resolve`. Implements `IBooleanResolvable` because zero (and NaN) is falsy.

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

Implicit IN lets handlers write `Data<number>.Ok(5)` without ceremony. Each implicit-IN call allocates one `@this` instance; that allocation is the cost we accept for class-style storage. No double-allocation: the primitive sits in the slot, not in a boxed object.

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

## `Parse` — the single string-coercion home

```csharp
public static @this? Parse(string s);
public static bool   TryParse(string s, out @this? n);
public static @this  Resolve(string raw, actor.context.@this context);
```

`Parse` picks the narrowest kind that fits losslessly:

1. **No decimal point, no exponent.** Try `long.TryParse`:
   - Fits in `int` → `Kind = Int`.
   - Otherwise → `Kind = Long`.
   - `long.TryParse` failed (out of int64 range) → fall through to decimal.
2. **Has decimal point, no exponent, no NaN/Infinity sigils.** Try `decimal.TryParse` → `Kind = Decimal`.
3. **Exponent present, NaN/Infinity literal, or decimal parse failed.** Try `double.TryParse` → `Kind = Double`.
4. **All fail.** Return `null` / `false`.

`Resolve(string, context)` is the source-generator-recognized factory — `app.types` catalog reads it (via reflection on the static method) to render `number` as a `scalar` with shape `string`, mirroring how `path` is rendered. Bare numeric literals in `set %x% = 3.14` flow through this entry point at lazy-materialization time, going through the `Serializers` registry. `Resolve` is a thin wrapper around `Parse` that attaches the `Context` and throws if `Parse` returns null (the action-site contract is "this must be a number").

## Arithmetic

`Add` / `Subtract` / `Multiply` / `Divide` / `Modulo` / `Power` all follow the same shape:

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

### Promotion table

| Left ↓ Right → | Int | Long | Decimal | Double |
|---|---|---|---|---|
| Int | Int | Long | Decimal | Double |
| Long | Long | Long | Decimal | Double |
| Decimal | Decimal | Decimal | Decimal | **policy.Precision** |
| Double | Double | Double | **policy.Precision** | Double |

The single fork is decimal × double. `policy.Precision == Double` (lenient default) promotes to Double; `Decimal` keeps decimal and throws on double NaN/Infinity/out-of-range. Same fork in both directions of the table.

### Integer overflow

`AddIntChecked` / `AddLongChecked` (and the multiply / power variants) use C# `checked` arithmetic:

- `policy.Overflow == Promote` (lenient default): on `OverflowException`, widen to the next kind. `Int` overflow → recompute as `Long`. `Long` overflow → recompute as `Decimal`. `Decimal` overflow → throw (no wider integer kind exists; decimal's range is far larger than long's, so this rarely fires).
- `policy.Overflow == Throw`: any overflow throws immediately, no widening. Int + Int that wouldn't fit in int → `OverflowException`.

Division by zero:
- Integer / Decimal: throws `DivideByZeroException` regardless of policy. There is no integer Infinity.
- Double: returns `double.PositiveInfinity` / `NegativeInfinity` / `NaN` per IEEE-754. Mixed kinds promoting to Double inherit this.

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

## Value equality

Two `number`s are equal if they represent the same mathematical value, regardless of `Kind`:

- `number.From(5) == number.From(5L)` → true.
- `number.From(5m) == number.From(5.0)` → true (within double precision).
- `number.From(0.1m + 0.2m) == number.From(0.3m)` → true.
- `number.From(0.1) + number.From(0.2) == number.From(0.3)` → **false** (IEEE-754 — `0.1 + 0.2 = 0.30000000000000004`).
- `number.From(double.NaN) == number.From(double.NaN)` → **false** (IEEE-754 rule).

```csharp
public override bool Equals(object? obj)
{
    if (obj is not @this other) return false;
    if (Kind == other.Kind)
        return Kind switch
        {
            NumberKind.Int or NumberKind.Long       => _i == other._i,
            NumberKind.Decimal                       => _d == other._d,
            NumberKind.Float or NumberKind.Double    => _f.Equals(other._f),  // NaN-aware
            _ => false
        };

    // Cross-kind: promote to the wider kind and compare. Bails out as inequality
    // on NaN/Infinity meets Decimal (those can never equal a finite decimal).
    var promoted = PromoteKind(Kind, other.Kind, NumberPolicy.Lenient);
    return promoted switch
    {
        NumberKind.Long    => AsInt64()   == other.AsInt64(),
        NumberKind.Decimal => AsDecimal() == other.AsDecimal(),  // throws if Double NaN/Infinity — caught, returns false
        NumberKind.Double  => AsDouble()  == other.AsDouble(),
        _ => false
    };
}

public override int GetHashCode() => Kind switch
{
    NumberKind.Int or NumberKind.Long       => HashCode.Combine(NumberKind.Long, _i),
    NumberKind.Decimal                       => HashCode.Combine(NumberKind.Decimal, _d),
    NumberKind.Float or NumberKind.Double    => HashCode.Combine(NumberKind.Double, _f),
    _ => 0
};
```

`==`/`!=` operator overloads delegate to `Equals`. Note that the hash code lumps `Int`/`Long` together (both via the long slot) and `Float`/`Double` together (both via the f slot) — same equality classes share hash codes. `Decimal` and the integer slot have different hash codes even for the same value (e.g., `From(5)` vs `From(5m)`) — this breaks the Equals/GetHashCode contract slightly for cross-kind equality. **Resolution at Stage 1**: canonicalize on hash — try to demote `5m` to `5L` for hash purposes. Worth flagging here; mechanical.

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
