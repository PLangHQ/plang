using System.Numerics;

namespace app.type.number;

/// <summary>
/// The C# scalar tower — Way 3's kind vocabulary and the promote/narrow
/// machinery. The kind <em>is</em> the value's exact CLR type; this file holds
/// the type↔kind maps, the three arithmetic categories (integer / binary-float /
/// decimal), and the integer narrowing ladder used by promote-then-narrow.
/// </summary>
public sealed partial class @this
{
    internal enum Category { Integer, BinaryFloat, Decimal }

    internal static Category CategoryOf(NumberKind k) => k switch
    {
        NumberKind.Half or NumberKind.Float or NumberKind.Double => Category.BinaryFloat,
        NumberKind.Decimal => Category.Decimal,
        _ => Category.Integer,
    };

    internal Category Cat => CategoryOf(Kind);

    // Kind ⇄ CLR type. The single source of truth is the boxed value's type;
    // these maps are the lookup both directions.
    internal static NumberKind ClrToKind(System.Type t)
    {
        if (t == typeof(sbyte)) return NumberKind.SByte;
        if (t == typeof(byte)) return NumberKind.Byte;
        if (t == typeof(short)) return NumberKind.Short;
        if (t == typeof(ushort)) return NumberKind.UShort;
        if (t == typeof(int)) return NumberKind.Int;
        if (t == typeof(uint)) return NumberKind.UInt;
        if (t == typeof(long)) return NumberKind.Long;
        if (t == typeof(ulong)) return NumberKind.ULong;
        if (t == typeof(Int128)) return NumberKind.Int128;
        if (t == typeof(UInt128)) return NumberKind.UInt128;
        if (t == typeof(BigInteger)) return NumberKind.BigInteger;
        if (t == typeof(Half)) return NumberKind.Half;
        if (t == typeof(float)) return NumberKind.Float;
        if (t == typeof(double)) return NumberKind.Double;
        if (t == typeof(decimal)) return NumberKind.Decimal;
        throw new System.InvalidOperationException($"number: {t.Name} is not a numeric CLR type.");
    }

    /// <summary>The PLang kind name for a CLR numeric type, or null if not numeric. Used by the Data stamp.</summary>
    public static string? KindNameForClr(System.Type clr)
    {
        try { return LabelOf(ClrToKind(clr)); }
        catch (System.InvalidOperationException) { return null; }
    }

    internal static string LabelOf(NumberKind k) => k switch
    {
        NumberKind.SByte => "sbyte",
        NumberKind.Byte => "byte",
        NumberKind.Short => "short",
        NumberKind.UShort => "ushort",
        NumberKind.Int => "int",
        NumberKind.UInt => "uint",
        NumberKind.Long => "long",
        NumberKind.ULong => "ulong",
        NumberKind.Int128 => "int128",
        NumberKind.UInt128 => "uint128",
        NumberKind.BigInteger => "biginteger",
        NumberKind.Half => "half",
        NumberKind.Float => "float",
        NumberKind.Double => "double",
        NumberKind.Decimal => "decimal",
        _ => "number",
    };

    internal static NumberKind? KindFromName(string? name) => name?.ToLowerInvariant() switch
    {
        "sbyte" => NumberKind.SByte,
        "byte" => NumberKind.Byte,
        "short" => NumberKind.Short,
        "ushort" => NumberKind.UShort,
        "int" or "integer" => NumberKind.Int,
        "uint" => NumberKind.UInt,
        "long" => NumberKind.Long,
        "ulong" => NumberKind.ULong,
        "int128" => NumberKind.Int128,
        "uint128" => NumberKind.UInt128,
        "biginteger" => NumberKind.BigInteger,
        "half" => NumberKind.Half,
        "float" => NumberKind.Float,
        "double" => NumberKind.Double,
        "decimal" => NumberKind.Decimal,
        _ => null,
    };

    internal static System.Type? KindToClrType(NumberKind? k) => k switch
    {
        NumberKind.SByte => typeof(sbyte),
        NumberKind.Byte => typeof(byte),
        NumberKind.Short => typeof(short),
        NumberKind.UShort => typeof(ushort),
        NumberKind.Int => typeof(int),
        NumberKind.UInt => typeof(uint),
        NumberKind.Long => typeof(long),
        NumberKind.ULong => typeof(ulong),
        NumberKind.Int128 => typeof(Int128),
        NumberKind.UInt128 => typeof(UInt128),
        NumberKind.BigInteger => typeof(BigInteger),
        NumberKind.Half => typeof(Half),
        NumberKind.Float => typeof(float),
        NumberKind.Double => typeof(double),
        NumberKind.Decimal => typeof(decimal),
        _ => null,
    };

    // Integer narrowing ladder, monotonic in max magnitude. Promote-then-narrow
    // picks the first rung at or above the floor (the wider operand kind) whose
    // [Min, Max] holds the BigInteger result. BigInteger is the unbounded top.
    private readonly record struct Rung(NumberKind Kind, BigInteger Min, BigInteger Max, bool Unbounded);

    private static readonly Rung[] IntegerLadder =
    {
        new(NumberKind.SByte, sbyte.MinValue, sbyte.MaxValue, false),
        new(NumberKind.Byte, byte.MinValue, byte.MaxValue, false),
        new(NumberKind.Short, short.MinValue, short.MaxValue, false),
        new(NumberKind.UShort, ushort.MinValue, ushort.MaxValue, false),
        new(NumberKind.Int, int.MinValue, int.MaxValue, false),
        new(NumberKind.UInt, uint.MinValue, uint.MaxValue, false),
        new(NumberKind.Long, long.MinValue, long.MaxValue, false),
        new(NumberKind.ULong, ulong.MinValue, ulong.MaxValue, false),
        new(NumberKind.Int128, (BigInteger)Int128.MinValue, (BigInteger)Int128.MaxValue, false),
        new(NumberKind.UInt128, (BigInteger)UInt128.MinValue, (BigInteger)UInt128.MaxValue, false),
        new(NumberKind.BigInteger, BigInteger.Zero, BigInteger.Zero, true),
    };

    private static int LadderIndex(NumberKind k)
    {
        for (int i = 0; i < IntegerLadder.Length; i++)
            if (IntegerLadder[i].Kind == k) return i;
        return IntegerLadder.Length - 1; // BigInteger
    }

    private static bool Fits(in Rung r, BigInteger v) => r.Unbounded || (v >= r.Min && v <= r.Max);

    // Signed-biased climb: when a result overflows its floor kind, widen along
    // the SIGNED track (int → long → Int128 → BigInteger) rather than into the
    // matching-width unsigned kind. This is what makes int+int overflow → long
    // (not uint) and uint+uint over-range → long (not ulong), per Way 3's
    // examples. The floor kind itself is kept when the value fits it (so a small
    // uint+uint stays uint).
    private static readonly NumberKind[] SignedClimb =
        { NumberKind.Int, NumberKind.Long, NumberKind.Int128, NumberKind.BigInteger };

    private static BigInteger MaxMagnitude(NumberKind k)
    {
        var r = IntegerLadder[LadderIndex(k)];
        return r.Unbounded ? BigInteger.Pow(2, 4096) : BigInteger.Max(BigInteger.Abs(r.Min), BigInteger.Abs(r.Max));
    }

    /// <summary>
    /// Narrow a BigInteger result. Keep the floor kind (wider of the operands)
    /// when the value fits it; otherwise widen along the signed track to the
    /// smallest signed kind that both exceeds the floor's range and holds the
    /// value — Way 3's "wider of operands, widened only on overflow", signed-biased.
    /// </summary>
    private static @this NarrowInteger(BigInteger v, NumberKind floor)
    {
        var floorRung = IntegerLadder[LadderIndex(floor)];
        if (Fits(floorRung, v)) return FromBigIntegerAs(v, floor);

        BigInteger floorMag = MaxMagnitude(floor);
        foreach (var k in SignedClimb)
        {
            if (MaxMagnitude(k) <= floorMag) continue;        // must be strictly wider than the floor
            if (Fits(IntegerLadder[LadderIndex(k)], v)) return FromBigIntegerAs(v, k);
        }
        return From(v); // BigInteger catch-all
    }

    /// <summary>Strict-width (OverflowMode.Throw): keep the floor kind; overflow throws.</summary>
    private static @this NarrowStrict(BigInteger v, NumberKind floor)
    {
        var r = IntegerLadder[LadderIndex(floor)];
        if (Fits(r, v))
            return floor == NumberKind.BigInteger ? From(v) : FromBigIntegerAs(v, floor);
        throw new System.OverflowException(
            $"Arithmetic result {v} does not fit kind '{LabelOf(floor)}' (overflow=throw / strict-width).");
    }

    private static @this FromBigIntegerAs(BigInteger v, NumberKind kind) => kind switch
    {
        NumberKind.SByte => From((sbyte)v),
        NumberKind.Byte => From((byte)v),
        NumberKind.Short => From((short)v),
        NumberKind.UShort => From((ushort)v),
        NumberKind.Int => From((int)v),
        NumberKind.UInt => From((uint)v),
        NumberKind.Long => From((long)v),
        NumberKind.ULong => From((ulong)v),
        NumberKind.Int128 => From((Int128)v),
        NumberKind.UInt128 => From((UInt128)v),
        _ => From(v),
    };

    /// <summary>The wider (higher-ladder) of two integer kinds — the narrowing floor.</summary>
    private static NumberKind WiderInteger(NumberKind a, NumberKind b)
        => LadderIndex(a) >= LadderIndex(b) ? a : b;
}
