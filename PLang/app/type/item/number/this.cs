using System.Numerics;

namespace app.type.item.number;

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
/// explicit precision choice (see <see cref="Precision"/>).</para>
///
/// <para>Storage is a single boxed CLR numeric (<see cref="BoxedValue"/>);
/// boxing was already the baseline since <c>Data.Value</c> is <c>object</c>.
/// The old <c>_i/_d/_f</c> tagged union and the <c>float→double</c> collapse
/// are gone.</para>
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Json))]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, System.IEquatable<@this>, System.IComparable<@this>, System.IComparable, System.IConvertible
{
    // The exact boxed CLR numeric — the single source of truth.
    private readonly object _value;

    /// <summary>The storage kind — CARRIED, set at birth by whoever mints (an implicit operator,
    /// <see cref="Parse"/>, the climb, the courier); never derived. Context-free stateless behavior;
    /// the value owns its kind, so <c>Write</c>/arithmetic never look it up.</summary>
    public kind.@this Kind { get; }

    private @this(object value, kind.@this kind) { _value = value; Kind = kind; }

    /// <summary>The exact boxed CLR numeric value (int, uint, BigInteger, Half, decimal, …).</summary>
    public object BoxedValue => _value;
    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => Kind.Write(this, w);

    /// <summary>A number's entity: the exact boxed CLR numeric as the mate, the kind name as kind.</summary>
    protected internal override global::app.type.@this Type
        => new("number", _value.GetType()) { Kind = new global::app.type.kind.@this(Kind.Name) };

    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "3.14";

    /// <summary>Catalog shape — number's wire form is a string-shaped scalar.</summary>
    public static string Shape => "string";

    // ── the 15 kind singletons — private immutable data (the sanctioned clause). Verbatim-lowercase
    //    names (@int, @long, …) = the kind tokens; PascalCase would shadow the CLR type names
    //    (Int128.MinValue, BigInteger.Pow) used across number/Ladder. ──
    private static readonly kind.@this @sbyte = new kind.@sbyte.@this();
    private static readonly kind.@this @byte = new kind.@byte.@this();
    private static readonly kind.@this @short = new kind.@short.@this();
    private static readonly kind.@this @ushort = new kind.@ushort.@this();
    private static readonly kind.@this @int = new kind.@int.@this();
    private static readonly kind.@this @uint = new kind.@uint.@this();
    private static readonly kind.@this @long = new kind.@long.@this();
    private static readonly kind.@this @ulong = new kind.@ulong.@this();
    private static readonly kind.@this int128 = new kind.int128.@this();
    private static readonly kind.@this uint128 = new kind.uint128.@this();
    private static readonly kind.@this half = new kind.half.@this();
    private static readonly kind.@this @float = new kind.@float.@this();
    private static readonly kind.@this @double = new kind.@double.@this();
    private static readonly kind.@this @decimal = new kind.@decimal.@this();
    private static readonly kind.@this biginteger = new kind.biginteger.@this();

    /// <summary>The 15 kind singletons by name — the declared-kind lookup (a sanctioned private data
    /// table); also the advertised vocabulary via <c>Kinds.Keys</c>.</summary>
    internal static readonly System.Collections.Generic.IReadOnlyDictionary<string, kind.@this> Kinds =
        new System.Collections.Generic.Dictionary<string, kind.@this>(System.StringComparer.OrdinalIgnoreCase)
        {
            [@sbyte.Name] = @sbyte, [@byte.Name] = @byte, [@short.Name] = @short, [@ushort.Name] = @ushort,
            [@int.Name] = @int, [@uint.Name] = @uint, [@long.Name] = @long, [@ulong.Name] = @ulong,
            [int128.Name] = int128, [uint128.Name] = uint128, [half.Name] = half,
            [@float.Name] = @float, [@double.Name] = @double, [@decimal.Name] = @decimal, [biginteger.Name] = biginteger,
        };

    // ── the CLR lifts — implicit operators, ALL 15 kinds; each names its own singleton (the typed
    //    lift, like bool's (@this)b / text's string operator). No From, no lookup at birth. ──
    public static implicit operator @this(sbyte v)   => new(v, @sbyte);
    public static implicit operator @this(byte v)    => new(v, @byte);
    public static implicit operator @this(short v)   => new(v, @short);
    public static implicit operator @this(ushort v)  => new(v, @ushort);
    public static implicit operator @this(int v)     => new(v, @int);
    public static implicit operator @this(uint v)    => new(v, @uint);
    public static implicit operator @this(long v)    => new(v, @long);
    public static implicit operator @this(ulong v)   => new(v, @ulong);
    public static implicit operator @this(Int128 v)  => new(v, int128);
    public static implicit operator @this(UInt128 v) => new(v, uint128);
    public static implicit operator @this(BigInteger v) => new(v, biginteger);
    public static implicit operator @this(Half v)    => new(v, half);
    public static implicit operator @this(float v)   => new(v, @float);
    public static implicit operator @this(double v)  => new(v, @double);
    public static implicit operator @this(decimal v) => new(v, @decimal);

    // ── THE PURE CORE — identical shape to bool/text/all 12. No exceptions on this path (the compare
    //    pass calls it: `"abc" == 5` → not-a-number → decline, never throw). Raw CLR keeps its kind
    //    (source fidelity via the lifts); a string's literal shape decides via Parse. ──
    // The pure core (the runtime boundary): a raw CLR numeric, a raw string, or an item of
    // another type flow through the SAME switch. An item unwraps to its own clr (a read, not a
    // Clr wrap); a raw CLR value is already its clr — a raw int hits `int v => v` with no shuttle.
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? clr = raw is global::app.type.item.@this it ? it.Clr<object>() : raw;
        return clr switch
        {
            string s => Parse(s),
            sbyte v => v, byte v => v, short v => v, ushort v => v,
            int v => v, uint v => v, long v => v, ulong v => v,
            Int128 v => v, UInt128 v => v, BigInteger v => v,
            Half v => v, float v => v, double v => v, decimal v => v,
            _ => null,
        };
    }

    // ── THE COURIER — the declared kind lives here. With no declared kind it is the pure core; with
    //    one, the kind builds it (from the typed ask's item) and the courier owns the error channel
    //    (a thrown reason → data.Fail, PRESERVED, never swallowed). ──
    public static @this? Create(object? raw, global::app.data.@this data)
    {
        var declared = data.Type?.Kind?.Name;
        if (declared is not null && raw is global::app.type.item.@this value)
        {
            if (!Kinds.TryGetValue(declared, out var kind))
            {
                data.Fail(new global::app.error.Error($"Unknown number kind '{declared}'.", "UnknownKind", 400));
                return null;
            }
            try { return kind.Create(value); }
            catch (System.Exception e) when (e is System.InvalidCastException or System.FormatException or System.OverflowException)
            {
                data.Fail(new global::app.error.Error(e.Message, "NumberConversionFailed", 400) { Exception = e });
                return null;
            }
        }
        if (Create(raw) is { } n) return n;
        data.Fail(new global::app.error.Error(
            $"Cannot convert {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name} to number.",
            "NumberConversionFailed", 400));
        return null;
    }

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
        _ => throw new System.InvalidOperationException($"number kind '{Kind.Name}' is not an exact integer."),
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
