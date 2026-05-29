namespace app.types.number;

/// <summary>
/// PLang <c>number</c> value — an immutable, kind-tagged scalar that unifies
/// CLR <c>int</c>/<c>long</c>/<c>decimal</c>/<c>float</c>/<c>double</c> under
/// one type. <c>int</c>/<c>long</c>/<c>decimal</c>/… are <em>kinds</em> of
/// <c>number</c>, not separate top-level PLang types — see
/// <see cref="Build"/> for the build-time kind derivation.
///
/// <para><c>sealed class</c> for codebase consistency with the rest of
/// <c>app/types/</c>; value semantics by way of <see cref="IEquatable{T}"/>
/// + value-equal <see cref="GetHashCode"/>. No <c>Context</c>, no
/// <see cref="modules.IContext"/> — a number has no use for per-request state
/// after construction.</para>
///
/// <para>Storage is a tagged union over three slots (<c>_i</c>, <c>_d</c>,
/// <c>_f</c>); exactly one is meaningful per <see cref="NumberKind"/>.
/// <see cref="NumberKind.Float"/> shares the <c>_f</c> slot with
/// <see cref="NumberKind.Double"/>: float widens to double on entry and
/// re-narrows at the explicit-OUT cast; the Float label keeps round-trip
/// identity for catalog/print.</para>
/// </summary>
public sealed partial class @this : System.IEquatable<@this>, global::app.data.IBooleanResolvable
{
    public NumberKind Kind { get; }

    // Tagged union — exactly one slot is meaningful per Kind.
    private readonly long _i;     // Int, Long
    private readonly decimal _d;  // Decimal
    private readonly double _f;   // Float (widened), Double

    private @this(NumberKind kind, long i = 0, decimal d = 0m, double f = 0.0)
    {
        Kind = kind; _i = i; _d = d; _f = f;
    }

    /// <summary>Catalog example — read via reflection by the schema builder.</summary>
    public static string Example => "3.14";

    /// <summary>Catalog shape — number's wire form is a string-shaped scalar.</summary>
    public static string Shape => "string";

    /// <summary>
    /// Developer-meaningful kind vocabulary the LLM catalog renders. Number is
    /// the one type that advertises its kinds; for every other type the kind
    /// is derivation-only (silent <see cref="Build"/> stamping).
    /// </summary>
    public static System.Collections.Generic.IReadOnlyList<string> Kinds { get; }
        = new[] { "int", "long", "decimal", "double" };

    // ---- Construction — static factories per kind ----

    public static @this From(int v) => new(NumberKind.Int, i: v);
    public static @this From(long v) => new(NumberKind.Long, i: v);
    public static @this From(decimal v) => new(NumberKind.Decimal, d: v);
    public static @this From(float v) => new(NumberKind.Float, f: v);
    public static @this From(double v) => new(NumberKind.Double, f: v);

    // Implicit IN — handlers can write `Data<number>.Ok(5)` without ceremony.
    public static implicit operator @this(int v) => From(v);
    public static implicit operator @this(long v) => From(v);
    public static implicit operator @this(decimal v) => From(v);
    public static implicit operator @this(float v) => From(v);
    public static implicit operator @this(double v) => From(v);

    // Explicit OUT — narrowing throws on failure, never silent corruption.
    public static explicit operator int(@this n) => n.ToInt32();
    public static explicit operator long(@this n) => n.ToInt64();
    public static explicit operator decimal(@this n) => n.ToDecimal();
    public static explicit operator double(@this n) => n.ToDouble();
    public static explicit operator float(@this n) => n.ToSingle();

    public int ToInt32() => Kind switch
    {
        NumberKind.Int => checked((int)_i),
        NumberKind.Long => checked((int)_i),
        NumberKind.Decimal => checked((int)_d),
        NumberKind.Double or NumberKind.Float =>
            double.IsNaN(_f) || double.IsInfinity(_f)
                ? throw new System.ArithmeticException("number is NaN or Infinity, cannot convert to int")
                : checked((int)_f),
        _ => throw new System.InvalidOperationException()
    };

    public long ToInt64() => Kind switch
    {
        NumberKind.Int or NumberKind.Long => _i,
        NumberKind.Decimal => checked((long)_d),
        NumberKind.Double or NumberKind.Float =>
            double.IsNaN(_f) || double.IsInfinity(_f)
                ? throw new System.ArithmeticException("number is NaN or Infinity, cannot convert to long")
                : checked((long)_f),
        _ => throw new System.InvalidOperationException()
    };

    public decimal ToDecimal() => Kind switch
    {
        NumberKind.Int or NumberKind.Long => _i,
        NumberKind.Decimal => _d,
        NumberKind.Double or NumberKind.Float =>
            double.IsNaN(_f) || double.IsInfinity(_f)
                ? throw new System.ArithmeticException("number is NaN or Infinity, cannot convert to decimal")
                : (decimal)_f,
        _ => throw new System.InvalidOperationException()
    };

    /// <summary>
    /// IEEE-754 has no failure mode for over-range — saturates to ±Infinity,
    /// which is a valid double. Lossy on Decimal past ~15 digits; never throws.
    /// </summary>
    public double ToDouble() => Kind switch
    {
        NumberKind.Int or NumberKind.Long => _i,
        NumberKind.Decimal => (double)_d,
        NumberKind.Double or NumberKind.Float => _f,
        _ => throw new System.InvalidOperationException()
    };

    public float ToSingle() => Kind switch
    {
        NumberKind.Int or NumberKind.Long => _i,
        NumberKind.Decimal => (float)_d,
        NumberKind.Double or NumberKind.Float => (float)_f,
        _ => throw new System.InvalidOperationException()
    };

    // ---- Internal slot accessors (used by the partial files) ----

    internal long AsInt64() => Kind switch
    {
        NumberKind.Int or NumberKind.Long => _i,
        NumberKind.Decimal => (long)_d,
        NumberKind.Double or NumberKind.Float => (long)_f,
        _ => throw new System.InvalidOperationException()
    };

    internal decimal AsDecimal() => Kind switch
    {
        NumberKind.Int or NumberKind.Long => _i,
        NumberKind.Decimal => _d,
        NumberKind.Double or NumberKind.Float => (decimal)_f,
        _ => throw new System.InvalidOperationException()
    };

    internal double AsDouble() => Kind switch
    {
        NumberKind.Int or NumberKind.Long => _i,
        NumberKind.Decimal => (double)_d,
        NumberKind.Double or NumberKind.Float => _f,
        _ => throw new System.InvalidOperationException()
    };

    // ---- IBooleanResolvable ----

    /// <summary>
    /// Zero is falsy; NaN is falsy; everything else is truthy. Synchronous —
    /// the async signature is inherited from <see cref="global::app.data.IBooleanResolvable"/>
    /// to allow type-defined probes (path's HEAD), but number's answer is local.
    /// </summary>
    public System.Threading.Tasks.Task<bool> AsBooleanAsync()
    {
        bool truthy = Kind switch
        {
            NumberKind.Int or NumberKind.Long => _i != 0,
            NumberKind.Decimal => _d != 0m,
            NumberKind.Double or NumberKind.Float => !double.IsNaN(_f) && _f != 0.0,
            _ => false
        };
        return System.Threading.Tasks.Task.FromResult(truthy);
    }

    public override string ToString() => Kind switch
    {
        NumberKind.Int or NumberKind.Long => _i.ToString(System.Globalization.CultureInfo.InvariantCulture),
        NumberKind.Decimal => _d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        NumberKind.Double or NumberKind.Float => _f.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
        _ => "(number?)"
    };
}

public enum NumberKind { Int, Long, Float, Double, Decimal }
