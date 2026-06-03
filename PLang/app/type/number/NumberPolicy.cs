namespace app.type.number;

/// <summary>
/// Arithmetic policy for <see cref="@this"/> (Way 3). Two orthogonal,
/// developer-facing axes — first-class knobs (math.* step params + config
/// cascade), NOT inert. The defaults ARE Way 3.
///
/// <para><see cref="Overflow"/>: <c>Promote</c> (default) computes integer ops
/// on a <c>BigInteger</c> carrier then narrows to the smallest kind that fits
/// — never wraps; <c>Throw</c> is strict-width (keep the operand kind, error if
/// the result doesn't fit). <see cref="Precision"/>: the <c>double ⊕ decimal</c>
/// mix is an <c>Error</c> by default (the developer must choose), with
/// <c>Double</c> / <c>Decimal</c> as the standing override.</para>
/// </summary>
public readonly struct NumberPolicy
{
    public OverflowMode Overflow { get; init; }
    public PrecisionMode Precision { get; init; }

    /// <summary>The Way-3 default — integers promote-then-narrow (no wrap); double⊕decimal errors.</summary>
    public static NumberPolicy Lenient => new()
    {
        Overflow = OverflowMode.Promote,
        Precision = PrecisionMode.Error,
    };

    /// <summary>Strict-width integers (no promotion); double⊕decimal still requires a choice.</summary>
    public static NumberPolicy Strict => new()
    {
        Overflow = OverflowMode.Throw,
        Precision = PrecisionMode.Error,
    };
}

/// <summary>
/// Integer overflow axis.
/// <c>Promote</c> (default): compute on a <c>BigInteger</c> carrier, narrow the
/// result to the smallest kind that holds it — <c>int+int</c> overflow → <c>long</c>,
/// <c>uint+uint</c> over-range → <c>long</c>. Never wraps.
/// <c>Throw</c>: strict-width — keep the operand kind; error if the result doesn't fit.
/// </summary>
public enum OverflowMode { Promote, Throw }

/// <summary>
/// Precision-mix axis (<c>double ⊕ decimal</c>). Neither type holds the other
/// exactly, so PLang refuses to pick silently.
/// <c>Error</c> (default): the developer must choose a mode for this mix.
/// <c>Double</c>: promote the mix to double (IEEE wins, decimal precision lost).
/// <c>Decimal</c>: promote to decimal (throws if the double is NaN/Infinity/out of range).
/// </summary>
public enum PrecisionMode { Error, Double, Decimal }
