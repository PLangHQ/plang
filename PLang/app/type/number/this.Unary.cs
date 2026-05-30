namespace app.type.number;

/// <summary>
/// Unary and comparison helpers — what <c>math.abs</c>, <c>math.floor</c>,
/// <c>math.ceiling</c>, <c>math.sqrt</c>, <c>math.round</c>, <c>math.min</c>,
/// <c>math.max</c> call. Each returns <see cref="global::app.data.@this{T}"/>
/// wrapping a <see cref="@this"/>; overflow / arithmetic exceptions surface
/// through the same <c>Wrap</c> envelope used by the arithmetic family —
/// <c>MathOverflow</c> / <c>ArithmeticError</c> keys.
///
/// <para>Abs preserves Kind; <c>Int.MinValue</c> would overflow as <c>int</c>
/// (checked) so the conversion lifts to Long first. Floor/Ceiling are
/// no-ops on Int/Long. Sqrt always returns Double (real-valued surface);
/// negative input throws <c>ArithmeticException</c> which <c>Wrap</c>
/// surfaces as <c>Data.Fail("ArithmeticError")</c> — one canonical error
/// key across both the direct call and the math.sqrt handler boundary.
/// Round preserves Kind for Int/Long; rounds Decimal/Double to
/// <c>decimals</c> places. Min/Max promote per <see cref="NumberPolicy"/>
/// exactly like the arithmetic family.</para>
/// </summary>
public sealed partial class @this
{
    public static global::app.data.@this<@this> Abs(@this a)
        => Wrap(() => DoAbs(a));

    public static global::app.data.@this<@this> Floor(@this a)
        => Wrap(() => DoFloor(a));

    public static global::app.data.@this<@this> Ceiling(@this a)
        => Wrap(() => DoCeiling(a));

    public static global::app.data.@this<@this> Sqrt(@this a)
        => Wrap(() => DoSqrt(a));

    public static global::app.data.@this<@this> Round(@this a, int decimals)
        => Wrap(() => DoRound(a, decimals));

    public static global::app.data.@this<@this> Min(@this a, @this b, NumberPolicy policy)
        => Wrap(() => DoMinMax(a, b, isMin: true, policy));

    public static global::app.data.@this<@this> Max(@this a, @this b, NumberPolicy policy)
        => Wrap(() => DoMinMax(a, b, isMin: false, policy));

    private static @this DoAbs(@this a) => a.Kind switch
    {
        // System.Math.Abs(int.MinValue) throws; lift to Long when the value
        // is at the edge so abs(int.MinValue) returns 2147483648 as Long
        // instead of a confusing OverflowException.
        NumberKind.Int when a.AsInt64() == int.MinValue => From(-(long)int.MinValue),
        NumberKind.Int => From(System.Math.Abs((int)a.AsInt64())),
        NumberKind.Long when a.AsInt64() == long.MinValue
            => throw new System.OverflowException("abs(long.MinValue) overflows the Long range."),
        NumberKind.Long => From(System.Math.Abs(a.AsInt64())),
        NumberKind.Decimal => From(System.Math.Abs(a.AsDecimal())),
        NumberKind.Float or NumberKind.Double => From(System.Math.Abs(a.AsDouble())),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DoFloor(@this a) => a.Kind switch
    {
        // Int/Long are already integral — no change.
        NumberKind.Int or NumberKind.Long => a,
        NumberKind.Decimal => From(System.Math.Floor(a.AsDecimal())),
        NumberKind.Float or NumberKind.Double => From(System.Math.Floor(a.AsDouble())),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DoCeiling(@this a) => a.Kind switch
    {
        NumberKind.Int or NumberKind.Long => a,
        NumberKind.Decimal => From(System.Math.Ceiling(a.AsDecimal())),
        NumberKind.Float or NumberKind.Double => From(System.Math.Ceiling(a.AsDouble())),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DoSqrt(@this a)
    {
        var d = a.AsDouble();
        if (d < 0)
            throw new System.ArithmeticException("Cannot take square root of a negative number.");
        return From(System.Math.Sqrt(d));
    }

    private static @this DoRound(@this a, int decimals) => a.Kind switch
    {
        NumberKind.Int or NumberKind.Long => a,
        NumberKind.Decimal => From(System.Math.Round(a.AsDecimal(), decimals, System.MidpointRounding.AwayFromZero)),
        NumberKind.Float or NumberKind.Double => From(System.Math.Round(a.AsDouble(), decimals, System.MidpointRounding.AwayFromZero)),
        _ => throw new System.InvalidOperationException(),
    };

    private static @this DoMinMax(@this a, @this b, bool isMin, NumberPolicy policy)
    {
        var kind = PromoteKind(a.Kind, b.Kind, policy);
        return kind switch
        {
            NumberKind.Int => From(isMin
                ? System.Math.Min((int)a.AsInt64(), (int)b.AsInt64())
                : System.Math.Max((int)a.AsInt64(), (int)b.AsInt64())),
            NumberKind.Long => From(isMin
                ? System.Math.Min(a.AsInt64(), b.AsInt64())
                : System.Math.Max(a.AsInt64(), b.AsInt64())),
            NumberKind.Decimal => From(isMin
                ? System.Math.Min(a.AsDecimal(), b.AsDecimal())
                : System.Math.Max(a.AsDecimal(), b.AsDecimal())),
            NumberKind.Float or NumberKind.Double => From(isMin
                ? System.Math.Min(a.AsDouble(), b.AsDouble())
                : System.Math.Max(a.AsDouble(), b.AsDouble())),
            _ => throw new System.InvalidOperationException(),
        };
    }
}
