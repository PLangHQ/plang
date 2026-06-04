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
        sbyte or byte or short or ushort or int or uint or long or ulong
            or Int128 or UInt128 or BigInteger or Half or float or double or decimal
            => Equals(FromObject(obj)),
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
        sbyte or byte or short or ushort or int or uint or long or ulong
            or Int128 or UInt128 or BigInteger or Half or float or double or decimal
            => CompareTo(FromObject(obj)),
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
