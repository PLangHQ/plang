namespace app.type.number;

/// <summary>
/// Lenient operator surface — <c>+ - * / %</c>, <c>== !=</c>. The throwing
/// path the policy-aware named methods (Stage 4) wrap. Divide and Power
/// leave the integer track (<c>7 / 2 → 3.5</c>); truncating division is
/// the explicit <c>math.intdiv</c> action.
///
/// <para>Promotion table for + - * %:
///   <c>Int × Int → Int</c>; either Long → Long; either Decimal → Decimal;
///   either Double → Double. Decimal × Double promotes to Double under
///   the lenient default (Stage 4 lets policy.Precision swing this to
///   Decimal). Same fork in both directions.</para>
/// </summary>
public sealed partial class @this
{
    public static @this operator +(@this a, @this b) => Lenient(a, b, Op.Add);
    public static @this operator -(@this a, @this b) => Lenient(a, b, Op.Sub);
    public static @this operator *(@this a, @this b) => Lenient(a, b, Op.Mul);
    public static @this operator /(@this a, @this b) => LenientDivide(a, b);
    public static @this operator %(@this a, @this b) => Lenient(a, b, Op.Mod);

    public static bool operator ==(@this? a, @this? b)
    {
        if (a is null) return b is null;
        return a.Equals(b);
    }

    public static bool operator !=(@this? a, @this? b) => !(a == b);

    private enum Op { Add, Sub, Mul, Mod }

    private static @this Lenient(@this a, @this b, Op op)
    {
        var kind = Promote(a.Kind, b.Kind);
        return kind switch
        {
            NumberKind.Int => OpInt(a.AsInt64(), b.AsInt64(), op, asInt: true),
            NumberKind.Long => OpInt(a.AsInt64(), b.AsInt64(), op, asInt: false),
            NumberKind.Decimal => OpDecimal(a.AsDecimal(), b.AsDecimal(), op),
            NumberKind.Double or NumberKind.Float => OpDouble(a.AsDouble(), b.AsDouble(), op),
            _ => throw new System.InvalidOperationException()
        };
    }

    private static @this LenientDivide(@this a, @this b)
    {
        // Divide leaves the integer track — promote int/long to decimal.
        var kind = Promote(a.Kind, b.Kind);
        if (kind == NumberKind.Int || kind == NumberKind.Long)
            kind = NumberKind.Decimal;
        return kind switch
        {
            NumberKind.Decimal => From(a.AsDecimal() / b.AsDecimal()),
            NumberKind.Double or NumberKind.Float => From(a.AsDouble() / b.AsDouble()),
            _ => throw new System.InvalidOperationException()
        };
    }

    private static @this OpInt(long a, long b, Op op, bool asInt)
    {
        long r = checked(op switch
        {
            Op.Add => a + b,
            Op.Sub => a - b,
            Op.Mul => a * b,
            Op.Mod => a % b,
            _ => throw new System.InvalidOperationException()
        });
        return asInt && r >= int.MinValue && r <= int.MaxValue
            ? From((int)r) : From(r);
    }

    private static @this OpDecimal(decimal a, decimal b, Op op) => op switch
    {
        Op.Add => From(a + b),
        Op.Sub => From(a - b),
        Op.Mul => From(a * b),
        Op.Mod => From(a % b),
        _ => throw new System.InvalidOperationException()
    };

    private static @this OpDouble(double a, double b, Op op) => op switch
    {
        Op.Add => From(a + b),
        Op.Sub => From(a - b),
        Op.Mul => From(a * b),
        Op.Mod => From(a % b),
        _ => throw new System.InvalidOperationException()
    };

    /// <summary>
    /// Promotion under the lenient default — decimal × double goes to double.
    /// Stage 4's policy-aware path lets the caller swing decimal × double the
    /// other way (<c>policy.Precision == Decimal</c>).
    /// </summary>
    private static NumberKind Promote(NumberKind a, NumberKind b)
    {
        if (a == NumberKind.Double || b == NumberKind.Double
            || a == NumberKind.Float || b == NumberKind.Float)
            return NumberKind.Double;
        if (a == NumberKind.Decimal || b == NumberKind.Decimal)
            return NumberKind.Decimal;
        if (a == NumberKind.Long || b == NumberKind.Long)
            return NumberKind.Long;
        return NumberKind.Int;
    }
}
