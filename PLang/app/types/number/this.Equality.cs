namespace app.types.number;

/// <summary>
/// Two equality verbs:
///   <see cref="Equals(@this?)"/> — lenient: cross-kind promote-and-compare
///     (<c>Int 5 == Long 5L == Decimal 5m == Double 5.0</c>). Non-transitive
///     at the precision boundary (decimal/double) — knowing trade.
///   <see cref="ExactEquals"/> — opt-in: same <see cref="NumberKind"/> AND
///     exact bits. The escape hatch for crypto/finance.
///
/// NaN never equals NaN (mirrors IEEE / CLR <see cref="double.Equals(double)"/>
/// non-NaN behavior on the operator level, while keeping value-equal hash
/// invariants intact).
///
/// Canonical hash code: integer-valued kinds share a bucket (so
/// <c>Int 5</c>, <c>Long 5L</c>, <c>Decimal 5m</c>, <c>Double 5.0</c> all
/// hash the same), preserving the <c>Equals ⇒ same hash</c> invariant for
/// the common case the lenient comparison binds. A non-integer-valued
/// decimal/double hashes via its native CLR hash.
/// </summary>
public sealed partial class @this
{
    public bool Equals(@this? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        // NaN never equals anything.
        if (IsNaN(this) || IsNaN(other)) return false;

        // Same-kind exact compare first (fast path + the only path the bit-
        // exact tests assert).
        if (Kind == other.Kind) return SameKindEquals(this, other);

        // Cross-kind: promote both to the wider numeric space and compare
        // without throwing on Decimal↔Double range mismatches.
        return CrossKindEquals(this, other);
    }

    public override bool Equals(object? obj) => obj switch
    {
        @this n => Equals(n),
        int or long or decimal or float or double or sbyte or byte or short or ushort or uint or ulong
            => Equals(FromObject(obj)),
        _ => false,
    };

    public bool ExactEquals(@this? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Kind != other.Kind) return false;
        if (IsNaN(this) || IsNaN(other)) return false;
        return SameKindEquals(this, other);
    }

    public override int GetHashCode()
    {
        // Integer-valued kinds bucket together so lenient `Equals ⇒ same hash`
        // holds for the common case (5, 5L, 5m, 5.0 all hash the same).
        if (TryIntegerValue(this, out var iv))
            return iv.GetHashCode();

        return Kind switch
        {
            NumberKind.Decimal => _d.GetHashCode(),
            NumberKind.Double or NumberKind.Float => _f.GetHashCode(),
            _ => _i.GetHashCode(),
        };
    }

    private static bool SameKindEquals(@this a, @this b) => a.Kind switch
    {
        NumberKind.Int or NumberKind.Long => a._i == b._i,
        NumberKind.Decimal => a._d == b._d,
        NumberKind.Double or NumberKind.Float => a._f.Equals(b._f),
        _ => false,
    };

    private static bool CrossKindEquals(@this a, @this b)
    {
        // Both integer-valued and inside long range → compare as long.
        bool aIsIntLike = a.Kind == NumberKind.Int || a.Kind == NumberKind.Long;
        bool bIsIntLike = b.Kind == NumberKind.Int || b.Kind == NumberKind.Long;
        if (aIsIntLike && bIsIntLike) return a._i == b._i;

        // Decimal ↔ Decimal-shaped: route through decimal.
        bool aDecLike = a.Kind == NumberKind.Decimal || aIsIntLike;
        bool bDecLike = b.Kind == NumberKind.Decimal || bIsIntLike;
        if (aDecLike && bDecLike) return a.AsDecimal() == b.AsDecimal();

        // Anything involving double: route through decimal when the double
        // is finite + in-range (guarded), else through double (lossy compare
        // but never throws).
        return DecimalGuardedEquals(a, b);
    }

    private static bool DecimalGuardedEquals(@this a, @this b)
    {
        if (TryToDecimal(a, out var ad) && TryToDecimal(b, out var bd))
            return ad == bd;
        return a.AsDouble() == b.AsDouble();
    }

    private static bool TryToDecimal(@this n, out decimal d)
    {
        d = 0m;
        switch (n.Kind)
        {
            case NumberKind.Int:
            case NumberKind.Long:
                d = n._i; return true;
            case NumberKind.Decimal:
                d = n._d; return true;
            case NumberKind.Double:
            case NumberKind.Float:
                if (double.IsNaN(n._f) || double.IsInfinity(n._f)) return false;
                if (n._f < (double)decimal.MinValue || n._f > (double)decimal.MaxValue) return false;
                try { d = (decimal)n._f; return true; }
                catch (System.OverflowException) { return false; }
        }
        return false;
    }

    private static bool TryIntegerValue(@this n, out long iv)
    {
        iv = 0;
        switch (n.Kind)
        {
            case NumberKind.Int:
            case NumberKind.Long:
                iv = n._i; return true;
            case NumberKind.Decimal:
                if (n._d == System.Math.Truncate(n._d)
                    && n._d >= long.MinValue && n._d <= long.MaxValue)
                {
                    iv = (long)n._d; return true;
                }
                return false;
            case NumberKind.Double:
            case NumberKind.Float:
                if (double.IsNaN(n._f) || double.IsInfinity(n._f)) return false;
                if (n._f != System.Math.Truncate(n._f)) return false;
                if (n._f < long.MinValue || n._f > long.MaxValue) return false;
                iv = (long)n._f; return true;
        }
        return false;
    }

    private static bool IsNaN(@this n) =>
        (n.Kind == NumberKind.Double || n.Kind == NumberKind.Float) && double.IsNaN(n._f);
}
