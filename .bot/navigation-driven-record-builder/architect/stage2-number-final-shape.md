# number — the final shape (reviewed with Ingi line-by-line)

**From:** architect. **Settled with Ingi (2026-07-10).** **This SUPERSEDES the number code shapes in `stage2-number-answer.md` and `stage2-number-context-free-answer.md`** — several of my earlier specifications were wrong (`Create(object)`/`Create(double)` CLR leaks, 15 typed `Create` wrappers, a missing pure core, a `From` corpse, a swallowed exception). This document is the reviewed whole. You own the final code; the two ⚠ items below are flagged for your verification, not asserted.

The pattern check that drove the corrections: **number follows the 12 exactly** — pure core + courier, nothing more — with its kind layer *underneath* the courier, not instead of the pattern.

---

## 1. `number/this.cs` — the value

```csharp
public sealed partial class @this : item.@this, ICreate<@this>
{
    private readonly object _value;      // the boxed storage (int/long/decimal/…)
    public kind.@this Kind { get; }      // the value CARRIES its kind instance — set at birth, never derived

    private @this(object value, kind.@this kind) { _value = value; Kind = kind; }

    // ── the 15 kind singletons — private immutable data (the sanctioned clause) ──
    private static readonly kind.@this KInt  = new kind.@int.@this();
    private static readonly kind.@this KLong = new kind.@long.@this();
    /* … all 15 … */
    internal static readonly IReadOnlyDictionary<string, kind.@this> Kinds =   // name → singleton
        new Dictionary<string, kind.@this>(OrdinalIgnoreCase) { [KInt.Name] = KInt, /* … */ };
    // NO clr→kind map — never needed: every birth site knows its kind (operators, Parse, climb, courier).

    // ── the CLR lifts — implicit operators, ALL 15 kinds (adds the missing Half/Int128/UInt128/BigInteger) ──
    public static implicit operator @this(int i)    => new(i, KInt);
    public static implicit operator @this(long l)   => new(l, KLong);
    public static implicit operator @this(double d) => new(d, KDouble);
    /* … all 15 — each operator names its own singleton: no lookup at birth …
       These ARE the typed lift (bool has (@this)b, text has its string operator) —
       there are NO Create(int)/Create(Half) wrapper methods. */

    // ── THE PURE CORE — identical shape to bool/text/all 12. No exceptions on this path. ──
    public static @this? Create(item.@this value)
    {
        if (value is @this self) return self;
        return value.Clr<object>() switch
        {
            string s => Parse(s),          // literal shape decides — see Parse
            int i => i,  long l => l,  double d => d,  decimal m => m,
            /* … remaining numeric arms — raw CLR keeps its kind: SOURCE FIDELITY via the lifts … */
            _ => null,                     // not number-shaped → decline. No Data, no error, no throw.
        };                                 // ← THE COMPARE PASS CALLS THIS ("abc" == 5 → not equal, not error)
    }

    // ── THE COURIER — declared kind lives here (the slot bool's courier doc reserved) ──
    public static @this? Create(item.@this value, data.@this data)
    {
        var declared = data.Type?.Kind?.Name;
        if (declared is null)
        {
            if (Create(value) is { } n) return n;
            data.Fail(new Error($"Cannot convert {value.Mint().Name} to number.", "NumberConversionFailed", 400));
            return null;
        }
        if (!Kinds.TryGetValue(declared, out var kind))
        {
            data.Fail(new Error($"Unknown number kind '{declared}'.", "UnknownKind", 400));
            return null;
        }
        try { return kind.Create(value); }              // the kind throws LOUD with the precise reason —
        catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException)
        {                                               // the courier is the seam that owns the error channel:
            data.Fail(new Error(e.Message, "NumberConversionFailed", 400) { Exception = e });
            return null;                                // reason PRESERVED on the binding — never swallowed
        }
    }

    // ── Parse — TryParse-shaped: detection IS production, one pass, never parses twice ──
    private static @this? Parse(string s)
    {
        if (long.TryParse(s, NumberStyles.Integer, Invariant, out var l))        return l;   // "42" → long
        if (BigInteger.TryParse(s, NumberStyles.Integer, Invariant, out var b))  return b;   // huge → bigint
        if (double.TryParse(s, NumberStyles.Float, Invariant, out var d))        return d;   // "4.2" → double
        return null;                                    // not a number — decline, NO exception
    }
}
```

## 2. `number/kind/this.cs` — the kind base

```csharp
public abstract class @this          // context-free, stateless, ctor takes NOTHING
{
    public abstract string Name { get; }

    // plang in, plang out. NON-nullable, NO default body (the FromObject default is DELETED),
    // NO catch — a value that can't be this kind throws loud via its own lower door;
    // the courier converts that to data.Fail. Only number's courier (and the climb) call this.
    public abstract number.@this Create(item.@this value);

    public abstract void Write(number.@this value, IWriter writer);                 // wire boundary
    public abstract item.@this Read<TReader>(ref TReader reader)                    // wire boundary
        where TReader : IReader, allows ref struct;

    public override string ToString() => Name;
}
```

## 3. The kinds — one line each; the 4 specials own their parse

```csharp
// number/kind/int/this.cs — the shape 11 of the 15 follow:
public sealed class @this : number.kind.@this
{
    public override string Name => "int";
    public override number.@this Create(item.@this value) => value.Clr<int>();   // value lowers ITSELF; throws precise
    public override void Write(number.@this v, IWriter w) => w.Int(v.ToInt32());
    public override item.@this Read<TReader>(ref TReader r) => (number.@this)r.Int();
}

// half — via double (its only C# road in):
public override number.@this Create(item.@this value) => (System.Half)value.Clr<double>();

// biginteger — CLR's converter can't reach it, so it owns its arms (throws precise, no catch):
public override number.@this Create(item.@this value)
    => value.Clr<object>() switch
    {
        BigInteger b => b,
        string s     => BigInteger.Parse(s, NumberStyles.Integer, Invariant),
        long l       => (BigInteger)l,   /* … the numeric arms … */
        var o        => throw new FormatException($"'{o}' cannot be biginteger."),
    };
// int128 / uint128 — same pattern as biginteger.
```

## 4. `number/this.Ladder.cs` — renamed, re-keyed, logic byte-for-byte

```csharp
private readonly record struct Level(string Kind, BigInteger Min, BigInteger Max, bool Unbounded)
{
    public bool Fits(BigInteger v) => Unbounded || (v >= Min && v <= Max);        // Fits ON the Level
}

private static readonly Level[] Ladder = { new("sbyte", …), /* … */ new("bigint", 0, 0, true) };
private static readonly string[] SignedClimb = { "int", "long", "int128", "bigint" };

// Narrow — private factory (sanctioned static): compute-wide happened upstream; place the result.
private static @this Narrow(BigInteger v, string floor)
{
    var floorLevel = Ladder[LadderIndex(floor)];
    if (floorLevel.Fits(v)) return Mint(v, floor);
    foreach (var k in SignedClimb)
    {
        if (MaxMagnitude(k) <= MaxMagnitude(floor)) continue;
        if (Ladder[LadderIndex(k)].Fits(v)) return Mint(v, k);
    }
    return v;                                            // BigInteger — the implicit lift
}
```

## 5. The serializers — the 15-arm switches become one-liners

```csharp
// number/serializer/Default.cs:   value.Kind.Write(value, writer);           // the value knows its kind
// number/serializer/Reader.cs:    number.Kinds[kindName].Read(ref reader);   // declared kind off the wire
```

---

## ⚠ Two flagged mechanics — verify, don't assume; stop and surface if they surprise

1. **`Mint(v, kindName)` in the climb** — turning the wide `BigInteger` into the landed level's storage. Recommendation: `Kinds[kindName].Create((@this)v)` — the level's kind builds it, no switch. **The open mechanic:** that path needs `number.Clr(target)` to lower a BigInteger backing to narrower CLR types (checked casts). That knowledge belongs in **number's own `Clr`** (its backing, its leaf) — today it hides inside `FromBigIntegerAs`'s switch, and the relocation target is number.Clr, never a kind-switch. If it gets messy in practice, stop and bring it back.
2. **The unary float rebuild** (Abs/Floor/Round): **inline at the call site** — `a.Kind.Create((@this)d)` — the implicit lift wraps the double, the operand's own kind rebuilds. Zero new names, no keyed helper. This openly **supersedes** the earlier "private helper keyed by kind name" line.

## Deletions this replaces

`NumberKind` enum · `CoerceToKind` · `KindFromName` · `ClrToKind`/`KindToClrType` · `From`/`FromObject`/`FromBigIntegerAs`/`FromDoubleAsKind` · the `Build(s)` literal-sniff (Parse owns it, one pass) · both 15-arm serializer switches · the clr→kind map (never needed) · `ClrForm` on number kinds (Ingi: zero readers once the base default and the clr→kind map died — it serves the ITEM kinds' collection door, which number kinds don't ride) · the 15 typed `Create(int…Half)` wrappers (never born — the operators are the lift).

## Acceptance

- Arithmetic suite untouched-green (the re-key + renames must be invisible).
- `%x% == "5"` / `"abc" == 5` comparison green through the pure core (no exceptions on that path).
- Declared-kind construction failures carry the **precise** reason on `data` (e.g. the exact bind message) — a test asserting the message is not generic.
- Grep-zero on every deleted name.
