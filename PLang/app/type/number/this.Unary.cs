using System.Numerics;

namespace app.type.number;

/// <summary>
/// Unary + min/max helpers — what <c>math.abs/floor/ceiling/sqrt/round/min/max</c>
/// call. Each returns <see cref="global::app.data.@this{T}"/> via the same
/// <c>Wrap</c> envelope as the arithmetic family. Abs/Floor/Ceiling/Round
/// preserve the input kind (Abs widens only if the magnitude overflows it);
/// Sqrt always returns double; Min/Max return whichever operand wins, keeping
/// its exact kind.
/// </summary>
public sealed partial class @this
{
    public static @this Abs(@this a) => Wrap(() => DoAbs(a));
    public static @this Floor(@this a) => Wrap(() => DoFloor(a));
    public static @this Ceiling(@this a) => Wrap(() => DoCeiling(a));
    public static @this Sqrt(@this a) => Wrap(() => DoSqrt(a));
    public static @this Round(@this a, @this decimals) => Wrap(() => DoRound(a, decimals));
    // Instance min/max — the op on the carrier; the other operand rides whole. No overflow/
    // precision axis (the winner keeps its exact kind), so no settings needed.
    public @this Min(@this b) => Wrap(() => this.CompareTo(b) <= 0 ? this : b);
    public @this Max(@this b) => Wrap(() => this.CompareTo(b) >= 0 ? this : b);

    private static @this FromDoubleAsKind(double m, NumberKind k) => k switch
    {
        NumberKind.Half => From((Half)m),
        NumberKind.Float => From((float)m),
        _ => From(m),
    };

    private static @this DoAbs(@this a) => a.Cat switch
    {
        // Promote-narrow: abs(int.MinValue) widens to long rather than throwing.
        Category.Integer => NarrowInteger(BigInteger.Abs(a.AsBigInteger()), a.Kind),
        Category.Decimal => From(System.Math.Abs(a.AsDecimal())),
        _ => FromDoubleAsKind(System.Math.Abs(a.AsDouble()), a.Kind),
    };

    private static @this DoFloor(@this a) => a.Cat switch
    {
        Category.Integer => a,
        Category.Decimal => From(System.Math.Floor(a.AsDecimal())),
        _ => FromDoubleAsKind(System.Math.Floor(a.AsDouble()), a.Kind),
    };

    private static @this DoCeiling(@this a) => a.Cat switch
    {
        Category.Integer => a,
        Category.Decimal => From(System.Math.Ceiling(a.AsDecimal())),
        _ => FromDoubleAsKind(System.Math.Ceiling(a.AsDouble()), a.Kind),
    };

    private static @this DoSqrt(@this a)
    {
        var d = a.AsDouble();
        if (d < 0) throw new System.ArithmeticException("Cannot take square root of a negative number.");
        return From(System.Math.Sqrt(d));
    }

    // number flows through; the int lowering happens ON the Math.Round lines —
    // the literal .NET edge, nowhere shallower.
    private static @this DoRound(@this a, @this decimals) => a.Cat switch
    {
        Category.Integer => a,
        Category.Decimal => From(System.Math.Round(a.AsDecimal(), decimals.ToInt32(), System.MidpointRounding.AwayFromZero)),
        _ => FromDoubleAsKind(System.Math.Round(a.AsDouble(), decimals.ToInt32(), System.MidpointRounding.AwayFromZero), a.Kind),
    };
}
