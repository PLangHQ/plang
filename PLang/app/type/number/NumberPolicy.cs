namespace app.type.number;

/// <summary>
/// Arithmetic policy for <see cref="@this"/>. Two orthogonal axes:
/// <see cref="Overflow"/> controls what happens when an Int/Long operation
/// exceeds its kind; <see cref="Precision"/> picks the result kind for
/// Decimal × Double mixes.
///
/// <para>Lenient defaults — "PLang sorts it." Strict is one
/// <c>- set math.number.overflow = throw</c> away for finance/crypto.</para>
/// </summary>
public readonly struct NumberPolicy
{
    public OverflowMode Overflow { get; init; }
    public PrecisionMode Precision { get; init; }

    public static NumberPolicy Lenient => new()
    {
        Overflow = OverflowMode.Promote,
        Precision = PrecisionMode.Double,
    };

    public static NumberPolicy Strict => new()
    {
        Overflow = OverflowMode.Throw,
        Precision = PrecisionMode.Decimal,
    };
}

/// <summary>
/// Overflow handling axis.
/// <c>Promote</c> (lenient default): Int overflow widens to Long; Long overflow
/// widens to Decimal; Decimal overflow throws (no wider integer kind exists).
/// <c>Throw</c>: any overflow throws immediately, no widening.
/// </summary>
public enum OverflowMode { Promote, Throw }

/// <summary>
/// Precision-mix axis (Decimal × Double).
/// <c>Double</c> (lenient default): promote to Double — IEEE-754 wins, decimal
/// precision lost past ~15 digits.
/// <c>Decimal</c>: stay Decimal — throws if the double operand is NaN /
/// Infinity / out of decimal range.
/// </summary>
public enum PrecisionMode { Double, Decimal }
