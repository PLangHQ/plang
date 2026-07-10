using System.Numerics;

namespace app.type.number;

/// <summary>
/// Equality + comparison over the tower.
///   <see cref="Equals(@this?)"/> — lenient cross-kind compare in the widest
///     common space (<c>5 == 5L == 5m == 5.0 == 5u</c>).
///   <see cref="ExactEquals"/> — same <see cref="Kind"/> AND exact value.
/// NaN never equals NaN. Integer-valued numbers across kinds share a hash
/// bucket so <c>Equals ⇒ same hash</c> holds for the common case.
/// </summary>
public sealed partial class @this
{
    // ---- Comparison (the unified hook — see app.type.compare) ----

    /// <summary>Outranks text — `text "10" > 9` compares numerically, not lexically.</summary>
    internal static int CompareRank => 30;

    /// <summary>
    /// Numeric comparison across the tower, in caller order. The driver coerces the
    /// non-numeric side via <see cref="FromObject"/> (text "10" → 10); a side that
    /// can't become a number makes the pair <see cref="global::app.data.Comparison.Incomparable"/>.
    /// </summary>
    public static global::app.data.Comparison Compare(object? a, object? b)
    {
        // The other side coerces into a number through the pure core (text "10" → 10); a value that
        // can't become a number → Incomparable. Operands ride as items (born-native); no exception.
        @this? na = a as @this ?? (a as global::app.type.item.@this is { } ia ? Create(ia) : null);
        @this? nb = b as @this ?? (b as global::app.type.item.@this is { } ib ? Create(ib) : null);
        if (na == null || nb == null) return global::app.data.Comparison.Incomparable;
        var c = na.CompareTo(nb);
        return c < 0 ? global::app.data.Comparison.Less
             : c > 0 ? global::app.data.Comparison.Greater
             : global::app.data.Comparison.Equal;
    }

    public bool Equals(@this? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (IsNaN(this) || IsNaN(other)) return false;

        if (Cat == Category.Integer && other.Cat == Category.Integer)
            return AsBigInteger() == other.AsBigInteger();

        // No binary float involved (integer/decimal mix) → exact decimal compare.
        if (Cat != Category.BinaryFloat && other.Cat != Category.BinaryFloat)
            return AsDecimal() == other.AsDecimal();

        // Binary float involved — compare in double (lossy but never throws).
        return AsDouble() == other.AsDouble();
    }

    public override bool Equals(object? obj) => obj switch
    {
        @this n => Equals(n),
        global::app.type.item.@this it => Create(it) is { } m && Equals(m),
        _ => false,
    };

    public int CompareTo(@this? other)
    {
        if (other is null) return 1;
        if (IsNaN(this)) return IsNaN(other) ? 0 : 1;
        if (IsNaN(other)) return -1;

        if (Cat == Category.Integer && other.Cat == Category.Integer)
            return AsBigInteger().CompareTo(other.AsBigInteger());
        if (Cat != Category.BinaryFloat && other.Cat != Category.BinaryFloat)
            return AsDecimal().CompareTo(other.AsDecimal());
        return AsDouble().CompareTo(other.AsDouble());
    }

    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        @this n => CompareTo(n),
        global::app.type.item.@this it when Create(it) is { } m => CompareTo(m),
        _ => throw new System.ArgumentException($"Cannot compare number to {obj.GetType()}"),
    };

    public bool ExactEquals(@this? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Kind != other.Kind) return false;
        if (IsNaN(this) || IsNaN(other)) return false;
        return _value.Equals(other._value);
    }

    public override int GetHashCode()
    {
        if (TryIntegerValue(this, out var iv)) return iv.GetHashCode();
        return AsDouble().GetHashCode();
    }

    private static bool TryIntegerValue(@this n, out BigInteger iv)
    {
        iv = BigInteger.Zero;
        switch (n.Cat)
        {
            case Category.Integer:
                iv = n.AsBigInteger(); return true;
            case Category.Decimal:
                var d = n.AsDecimal();
                if (d == System.Math.Truncate(d)) { iv = (BigInteger)d; return true; }
                return false;
            default:
                var f = n.AsDouble();
                if (double.IsNaN(f) || double.IsInfinity(f)) return false;
                if (f != System.Math.Truncate(f)) return false;
                iv = (BigInteger)f; return true;
        }
    }

    private static bool IsNaN(@this n) => n.Cat == Category.BinaryFloat && double.IsNaN(n.AsDouble());
}
