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
/// <para>Storage is a single boxed CLR numeric (<see cref="BoxedValue"/>) — no
/// tagged union, and a float stays a float.</para>
/// </summary>
[global::app.Attributes.PlangType("number")]
public sealed partial class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>, System.IEquatable<@this>, System.IComparable<@this>, System.IComparable, System.IConvertible
{
    // The exact boxed CLR numeric — the single source of truth.
    private readonly object _value;

    /// <summary>The storage kind — its type's kind (<c>{number, int}</c>'s <c>int</c>): stateless behavior the value
    /// asks, so <c>Write</c>/arithmetic never look it up.</summary>
    // One narrowing, kept for the kind's arithmetic (precision, the climb) — it goes with the kinds item, where
    // number's kinds become the app's registered kinds.
    public kind.@this Kind => (kind.@this)Type.kind;

    // The operator's birth (the 15 CLR lifts, Parse with no type in hand, a kind's Create/Read, a result whose kind
    // climbed): no app to ask, so it holds {number, <kind>} built from the class, carrying the kind.
    private @this(object value, kind.@this kind)
        : base(new global::app.type.@this("number", typeof(@this)) { kind = kind }) => _value = value;

    /// <summary>The number <paramref name="source"/> is, born holding <paramref name="type"/> — the app's
    /// <c>{number, &lt;its kind&gt;}</c>, handed in by its maker (the courier, a reader, a derived result).</summary>
    internal @this(global::app.type.@this type, @this source) : base(type) => _value = source._value;

    /// <summary>The empty number, 0 as a long — what a number holds before anything is in it (type.Empty). Its kind is
    /// long, which the plain number type type.Empty holds is not, so it is born as the operator births one.</summary>
    internal @this() : this(0L, @long) { }

    /// <summary>The exact boxed CLR numeric value (int, uint, BigInteger, Half, decimal, …).</summary>
    public object BoxedValue => _value;
    public override bool IsLeaf => true;
    /// <summary>A number writes its value through the writer's number primitives, as the C# number it is held as: the
    /// small widths as an int, the wide ones as a long while they fit, a half as a double; one no primitive holds
    /// (past a long, a 128-bit or big integer) as its digits. The writer decides how each looks.</summary>
    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter w)
    {
        switch (_value)
        {
            case int n: w.Int(n); break;
            case long n: w.Long(n); break;
            case float n: w.Float(n); break;
            case double n: w.Double(n); break;
            case decimal n: w.Decimal(n); break;
            case byte n: w.Int(n); break;
            case sbyte n: w.Int(n); break;
            case short n: w.Int(n); break;
            case ushort n: w.Int(n); break;
            case uint n: w.Long(n); break;
            case ulong n when n <= long.MaxValue: w.Long((long)n); break;
            case System.Half n: w.Double((double)n); break;
            default: w.String(System.Convert.ToString(_value, System.Globalization.CultureInfo.InvariantCulture)!); break;
        }
        return default;
    }

    /// <summary>Catalog example — read via reflection by the schema builder.</summary>

    /// <summary>Catalog shape — number's wire form is a string-shaped scalar.</summary>
    public static string Shape => "string";

    // ── the 15 kind singletons — the operator births' kinds only (a CLR lift has no app; every number born with an
    //    app holds its app's registered kind). Verbatim-lowercase
    //    names (@int, @long, …) = the kind tokens; PascalCase would shadow the CLR type names
    //    (Int128.MinValue, BigInteger.Pow) used across number/Ladder. Each a kind of the one class-built number type. ──
    private static readonly global::app.type.@this family = new("number", typeof(@this));
    private static readonly kind.@this @sbyte = new kind.@sbyte.@this(family);
    private static readonly kind.@this @byte = new kind.@byte.@this(family);
    private static readonly kind.@this @short = new kind.@short.@this(family);
    private static readonly kind.@this @ushort = new kind.@ushort.@this(family);
    private static readonly kind.@this @int = new kind.@int.@this(family);
    private static readonly kind.@this @uint = new kind.@uint.@this(family);
    private static readonly kind.@this @long = new kind.@long.@this(family);
    private static readonly kind.@this @ulong = new kind.@ulong.@this(family);
    private static readonly kind.@this int128 = new kind.int128.@this(family);
    private static readonly kind.@this uint128 = new kind.uint128.@this(family);
    private static readonly kind.@this half = new kind.half.@this(family);
    private static readonly kind.@this @float = new kind.@float.@this(family);
    private static readonly kind.@this @double = new kind.@double.@this(family);
    private static readonly kind.@this @decimal = new kind.@decimal.@this(family);
    private static readonly kind.@this biginteger = new kind.biginteger.@this(family);

    /// <summary>The 15 kind singletons by name — what an operator-born number (no app) names its kinds by; a number
    /// with an app asks its family.</summary>
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
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        var asked = (type ?? data.Type).kind;
        var named = asked is { IsEmpty: false } k ? k.Name : null;
        // Pass-through — a number already of the declared kind (or with none declared) rides out as it is.
        if (raw is @this same && (named is null || string.Equals(same.Kind.Name, named, System.StringComparison.OrdinalIgnoreCase)))
            return same;
        if (named is not null)
        {
            // the kind declared is the app's (its type's own kind), else the app's number entry names it
            if ((asked as kind.@this ?? data.Context?.App.type.list[typeof(@this)].kind[named] as kind.@this) is not { } kind)
            {
                data.Fail(new global::app.error.@this($"Unknown number kind '{named}'.", "UnknownKind", 400));
                return null;
            }
            // The declared kind builds from the value as it is — an item itself, a raw string as the text
            // it is (so "3000000000" as uint is read as a uint, never through a long), a raw C# number as
            // the number it is.
            var value = raw switch
            {
                global::app.type.item.@this item => item,
                string s => (global::app.type.item.text.@this)s,
                _ => Create(raw),
            };
            if (value is null)
            {
                data.Fail(new global::app.error.@this(
                    $"Cannot convert {raw?.GetType().Name} to number.", "NumberConversionFailed", 400));
                return null;
            }
            try { return Retype(kind.Create(value)); }
            // a value the kind can't hold (a fraction as an integer kind, NaN) says why under its own key
            catch (global::app.error.AppException e)
            {
                data.Fail(e.Error);
                return null;
            }
            catch (System.Exception e) when (e is System.InvalidCastException or System.FormatException or System.OverflowException)
            {
                data.Fail(new global::app.error.@this(e.Message, "NumberConversionFailed", 400) { Exception = e });
                return null;
            }
        }
        if (Create(raw) is { } n) return ReferenceEquals(n, raw) ? n : Retype(n);
        data.Fail(new global::app.error.@this(
            $"Cannot convert {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name} to number.",
            "NumberConversionFailed", 400));
        return null;

        // gives a number made here the app's {number, <its kind>}, as the binding's asker names the app (a binding with
        // no context, the operator's, keeps the operator's birth)
        @this Retype(@this number) => data.Context is { } context
            ? new(context.App.type.list[new global::app.type.@this("number", number.Kind.Name), context], number)
            : number;
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
            throw new global::app.error.AppException(
                $"Number {ToString()} has a fractional part and cannot convert to {target.Name} — round it first (math.round / math.floor).",
                "FractionalToInteger", 400);
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
        _ => throw new global::app.error.AppException($"number {ToString()} (kind '{Kind.Name}') is not an exact integer.", "NotAnInteger", 400),
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
        _ => double.IsNaN(AsDouble()) || double.IsInfinity(AsDouble()) ? throw NotFinite("int") : checked((int)AsDouble()),
    };

    public long ToInt64() => Cat switch
    {
        Category.Integer => checked((long)AsBigInteger()),
        Category.Decimal => checked((long)AsDecimal()),
        _ => double.IsNaN(AsDouble()) || double.IsInfinity(AsDouble()) ? throw NotFinite("long") : checked((long)AsDouble()),
    };

    public decimal ToDecimal() => Cat switch
    {
        Category.Integer => (decimal)AsBigInteger(),
        Category.Decimal => AsDecimal(),
        _ => double.IsNaN(AsDouble()) || double.IsInfinity(AsDouble()) ? throw NotFinite("decimal") : (decimal)AsDouble(),
    };

    // NaN and ±Infinity have no integer or decimal form — the program's value, keyed.
    private global::app.error.AppException NotFinite(string target)
        => new($"number {ToString()} is NaN or Infinity and cannot convert to {target}", "NumberNotFinite", 400);

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
