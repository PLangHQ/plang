using System.Numerics;

namespace app.type.item.number;

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
    // The number's own operations — the op on the carrier, another operand riding whole.
    [LlmBuilder] public @this Abs() => Wrap(() => DoAbs(this));
    [LlmBuilder] public @this Floor() => Wrap(() => DoFloor(this));
    [LlmBuilder] public @this Ceiling() => Wrap(() => DoCeiling(this));
    [LlmBuilder] public @this Sqrt() => Wrap(() => DoSqrt(this));
    [LlmBuilder] public @this Round(@this decimals) => Wrap(() => DoRound(this, decimals));
    // No overflow/precision axis for min/max (the winner keeps its exact kind), so no settings needed.
    [LlmBuilder] public @this Min(@this b) => Wrap(() => this.CompareTo(b) <= 0 ? this : b);
    [LlmBuilder] public @this Max(@this b) => Wrap(() => this.CompareTo(b) >= 0 ? this : b);

    // A binary-float unary result rebuilds at the operand's OWN kind: the implicit lift wraps the
    // computed double, the operand's kind rebuilds it at its size (half narrows, float narrows). No
    // FromDoubleAsKind helper — the kind owns "build a number of my size" (Create).
    private static @this DoAbs(@this a) => a.Cat switch
    {
        // Promote-narrow: abs(int.MinValue) widens to long rather than throwing.
        Category.Integer => Narrow(BigInteger.Abs(a.AsBigInteger()), a.Kind.Name),
        Category.Decimal => (@this)System.Math.Abs(a.AsDecimal()),
        _ => a.Kind.Create((@this)System.Math.Abs(a.AsDouble())),
    };

    private static @this DoFloor(@this a) => a.Cat switch
    {
        Category.Integer => a,
        Category.Decimal => (@this)System.Math.Floor(a.AsDecimal()),
        _ => a.Kind.Create((@this)System.Math.Floor(a.AsDouble())),
    };

    private static @this DoCeiling(@this a) => a.Cat switch
    {
        Category.Integer => a,
        Category.Decimal => (@this)System.Math.Ceiling(a.AsDecimal()),
        _ => a.Kind.Create((@this)System.Math.Ceiling(a.AsDouble())),
    };

    private static @this DoSqrt(@this a)
    {
        var d = a.AsDouble();
        if (d < 0) throw new System.ArithmeticException("Cannot take square root of a negative number.");
        return (@this)System.Math.Sqrt(d);
    }

    // number flows through; the int lowering happens ON the Math.Round lines —
    // the literal .NET edge, nowhere shallower.
    private static @this DoRound(@this a, @this decimals) => a.Cat switch
    {
        Category.Integer => a,
        Category.Decimal => (@this)System.Math.Round(a.AsDecimal(), decimals.ToInt32(), System.MidpointRounding.AwayFromZero),
        _ => a.Kind.Create((@this)System.Math.Round(a.AsDouble(), decimals.ToInt32(), System.MidpointRounding.AwayFromZero)),
    };
}
