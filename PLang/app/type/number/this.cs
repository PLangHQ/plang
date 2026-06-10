using System.Numerics;

namespace app.type.number;

/// <summary>
/// PLang <c>number</c> value (Way 3) — an immutable scalar that holds its
/// <em>exact</em> CLR numeric value; the <see cref="Kind"/> <em>is</em> that
/// value's CLR type (no separate stored label to drift). The full C# scalar
/// tower is supported as kinds: <c>sbyte byte short ushort int uint long ulong
/// Int128 UInt128 Half float double decimal BigInteger</c>.
///
/// <para>Arithmetic promotes operands to a carrier that cannot lose
/// (<c>BigInteger</c> for integers, <c>double</c> for binary floats,
/// <c>decimal</c> for base-10), then narrows the result to the smallest kind
/// that fits — so <c>3000000000u + 2000000000u</c> lands as <c>5000000000</c>
/// (a <c>long</c>) rather than wrapping. <c>double ⊕ decimal</c> requires an
/// explicit precision choice (see <see cref="NumberPolicy"/>).</para>
///
/// <para>Storage is a single boxed CLR numeric (<see cref="BoxedValue"/>);
/// boxing was already the baseline since <c>Data.Value</c> is <c>object</c>.
/// The old <c>_i/_d/_f</c> tagged union and the <c>float→double</c> collapse
/// are gone.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, System.IEquatable<@this>, System.IComparable<@this>, System.IComparable, System.IConvertible
{
    // The exact boxed CLR numeric — the single source of truth. Kind derives from its type.
    private readonly object _value;

    private @this(object value) { _value = value; }

    /// <summary>The exact boxed CLR numeric value (int, uint, BigInteger, Half, decimal, …).</summary>
    public object BoxedValue => _value;
    public override object? ToRaw() => _value;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => global::app.type.number.serializer.Default.Write(this, w);

    /// <summary>The kind — derived from the boxed value's CLR type, never stored separately.</summary>
    public NumberKind Kind => ClrToKind(_value.GetType());

    /// <summary>The PLang kind name ("int", "uint", "biginteger", "half", …).</summary>
    public string KindLabel => LabelOf(Kind);

    /// <summary>A number's entity: the exact boxed CLR numeric as the mate,
    /// the precision as kind — the full scalar tower, no collapse.</summary>
    protected internal override global::app.type.@this Mint()
        => new("number", _value.GetType()) { Kind = KindLabel };

    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "3.14";

    /// <summary>Catalog shape — number's wire form is a string-shaped scalar.</summary>
    public static string Shape => "string";

    /// <summary>
    /// The advertised kind vocabulary — the full C# scalar tower. Distinct from
    /// <see cref="Kind"/> (the per-value derived kind).
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<string> Kinds { get; }
        = new[]
        {
            "sbyte", "byte", "short", "ushort", "int", "uint", "long", "ulong",
            "int128", "uint128", "half", "float", "double", "decimal", "biginteger",
        };

    // ---- Construction — one factory per CLR type ----

    public static @this From(sbyte v) => new(v);
    public static @this From(byte v) => new(v);
    public static @this From(short v) => new(v);
    public static @this From(ushort v) => new(v);
    public static @this From(int v) => new(v);
    public static @this From(uint v) => new(v);
    public static @this From(long v) => new(v);
    public static @this From(ulong v) => new(v);
    public static @this From(Int128 v) => new(v);
    public static @this From(UInt128 v) => new(v);
    public static @this From(BigInteger v) => new(v);
    public static @this From(Half v) => new(v);
    public static @this From(float v) => new(v);
    public static @this From(double v) => new(v);
    public static @this From(decimal v) => new(v);

    /// <summary>
    /// Polymorphic coercion — accepts any CLR numeric (boxed verbatim, exact
    /// kind preserved), a string (routed through <see cref="Parse"/>), or an
    /// existing <see cref="@this"/>. Throws <see cref="System.FormatException"/>
    /// for non-numbers; null returns null.
    /// </summary>
    public static @this? FromObject(object? value)
    {
        switch (value)
        {
            case null: return null;
            case @this n: return n;
            case global::app.type.text.@this t:
                return Parse(t.Value)
                    ?? throw new System.FormatException($"number.FromObject: '{t.Value}' is not a valid number");
            case sbyte v: return From(v);
            case byte v: return From(v);
            case short v: return From(v);
            case ushort v: return From(v);
            case int v: return From(v);
            case uint v: return From(v);
            case long v: return From(v);
            case ulong v: return From(v);
            case Int128 v: return From(v);
            case UInt128 v: return From(v);
            case BigInteger v: return From(v);
            case Half v: return From(v);
            case float v: return From(v);
            case double v: return From(v);
            case decimal v: return From(v);
            case string s:
                var parsed = Parse(s);
                if (parsed == null) throw new System.FormatException($"number.FromObject: '{s}' is not a valid number");
                return parsed;
            case bool: throw new System.FormatException("number.FromObject: bool is not a number");
            default:
                throw new System.FormatException(
                    $"number.FromObject: cannot coerce {value.GetType().Name} to number");
        }
    }

    // Implicit IN — handlers can write `Data<number>.Ok(5)` without ceremony.
    public static implicit operator @this(int v) => From(v);
    public static implicit operator @this(long v) => From(v);
    public static implicit operator @this(decimal v) => From(v);
    public static implicit operator @this(float v) => From(v);
    public static implicit operator @this(double v) => From(v);

    /// <summary>The CLR exit door — number hands its own boxed backing; the
    /// tower's loss policy applies first: a fractional value narrowing to an
    /// integral target is an ERROR (state intent with math.round/floor), never
    /// a silent round/truncate.</summary>
    internal override object? Clr(System.Type target)
    {
        if ((target == typeof(int) || target == typeof(long) || target == typeof(short)
             || target == typeof(byte) || target == typeof(sbyte) || target == typeof(uint)
             || target == typeof(ulong) || target == typeof(ushort) || target == typeof(BigInteger))
            && Cat != Category.Integer && AsDecimalLossy() % 1m != 0m)
            throw new System.InvalidCastException(
                $"Number {ToString()} has a fractional part and cannot convert to {target.Name} — round it first (math.round / math.floor).");
        return ClrConvert(_value, target);
    }

    // The widest lossy carrier for the fraction check only — never returned.
    private decimal AsDecimalLossy() => Cat == Category.Decimal ? AsDecimal() : (decimal)AsDouble();

    // Explicit OUT — narrowing throws on failure, never silent corruption.
    public static explicit operator int(@this n) => n.ToInt32();
    public static explicit operator long(@this n) => n.ToInt64();
    public static explicit operator decimal(@this n) => n.ToDecimal();
    public static explicit operator double(@this n) => n.ToDouble();
    public static explicit operator float(@this n) => n.ToSingle();

    // ---- Raw widest-carrier accessors (used across the partials) ----

    internal BigInteger AsBigInteger() => _value switch
    {
        sbyte v => v, byte v => v, short v => v, ushort v => v,
        int v => v, uint v => v, long v => v, ulong v => v,
        Int128 v => (BigInteger)v, UInt128 v => (BigInteger)v, BigInteger v => v,
        decimal d when d == System.Math.Truncate(d) => (BigInteger)d,
        _ => throw new System.InvalidOperationException($"number kind '{KindLabel}' is not an exact integer."),
    };

    internal decimal AsDecimal() => _value switch
    {
        sbyte v => v, byte v => v, short v => v, ushort v => v,
        int v => v, uint v => v, long v => v, ulong v => v,
        Int128 v => (decimal)v, UInt128 v => (decimal)v, BigInteger v => (decimal)v,
        Half v => (decimal)(double)v, float v => (decimal)v, double v => (decimal)v, decimal v => v,
        _ => throw new System.InvalidOperationException(),
    };

    internal double AsDouble() => _value switch
    {
        sbyte v => v, byte v => v, short v => v, ushort v => v,
        int v => v, uint v => v, long v => v, ulong v => v,
        Int128 v => (double)v, UInt128 v => (double)v, BigInteger v => (double)v,
        Half v => (double)v, float v => v, double v => v, decimal v => (double)v,
        _ => throw new System.InvalidOperationException(),
    };

    // ---- Public typed conversions ----

    public int ToInt32() => Cat switch
    {
        Category.Integer => checked((int)AsBigInteger()),
        Category.Decimal => checked((int)AsDecimal()),
        _ => double.IsNaN(AsDouble()) || double.IsInfinity(AsDouble())
            ? throw new System.ArithmeticException("number is NaN or Infinity, cannot convert to int")
            : checked((int)AsDouble()),
    };

    public long ToInt64() => Cat switch
    {
        Category.Integer => checked((long)AsBigInteger()),
        Category.Decimal => checked((long)AsDecimal()),
        _ => double.IsNaN(AsDouble()) || double.IsInfinity(AsDouble())
            ? throw new System.ArithmeticException("number is NaN or Infinity, cannot convert to long")
            : checked((long)AsDouble()),
    };

    public decimal ToDecimal() => Cat switch
    {
        Category.Integer => (decimal)AsBigInteger(),
        Category.Decimal => AsDecimal(),
        _ => double.IsNaN(AsDouble()) || double.IsInfinity(AsDouble())
            ? throw new System.ArithmeticException("number is NaN or Infinity, cannot convert to decimal")
            : (decimal)AsDouble(),
    };

    /// <summary>IEEE-754 saturates over-range to ±Infinity; never throws.</summary>
    public double ToDouble() => AsDouble();

    public float ToSingle() => (float)AsDouble();

    /// <summary>The exact BigInteger value of an integer-kind number.</summary>
    public BigInteger ToBigInteger() => AsBigInteger();

    // ---- Truthiness (item) ----

    /// <summary>Zero is falsy; NaN is falsy; everything else is truthy.</summary>
    public override bool IsTruthy() => Cat switch
    {
        Category.Integer => AsBigInteger() != BigInteger.Zero,
        Category.Decimal => AsDecimal() != 0m,
        _ => !double.IsNaN(AsDouble()) && AsDouble() != 0.0,
    };

    public override string ToString() => _value switch
    {
        decimal d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Half h => ((double)h).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        float f => f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        double db => db.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        System.IFormattable fmt => fmt.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => _value.ToString() ?? "(number?)",
    };
}

/// <summary>The full C# scalar tower as number kinds. The kind is derived from the value's CLR type.</summary>
public enum NumberKind
{
    SByte, Byte, Short, UShort, Int, UInt, Long, ULong,
    Int128, UInt128, BigInteger, Half, Float, Double, Decimal,
}
