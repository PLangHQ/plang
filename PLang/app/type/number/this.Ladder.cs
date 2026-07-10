using System.Numerics;

namespace app.type.number;

/// <summary>
/// The Ladder — the cross-kind RELATION (which size an overflow climbs to), the one place that knows
/// the ORDER between kinds. "int overflows to long" is not int's knowledge nor long's — it's knowledge
/// about the order, so it lives here, keyed by kind NAME (the kind's identity). A LEVEL owns its range
/// and answers its own <see cref="Level.Fits"/>. Logic is byte-for-byte the old tower; only the key
/// type (NumberKind enum → name token) and the names (Tower→Ladder, Rung→Level, NarrowInteger→Narrow)
/// changed. The three arithmetic categories (integer / binary-float / decimal) live here too.
/// </summary>
public sealed partial class @this
{
    internal enum Category { Integer, BinaryFloat, Decimal }

    internal static Category CategoryOf(string kind) => kind switch
    {
        "half" or "float" or "double" => Category.BinaryFloat,
        "decimal" => Category.Decimal,
        _ => Category.Integer,
    };

    internal Category Cat => CategoryOf(Kind.Name);

    // A LEVEL — a rung of the integer ladder, keyed by its kind NAME, owning its own range question.
    private readonly record struct Level(string Kind, BigInteger Min, BigInteger Max, bool Unbounded)
    {
        public bool Fits(BigInteger v) => Unbounded || (v >= Min && v <= Max);
    }

    // ONE ladder, monotonic in max magnitude; BigInteger is the unbounded top (fractional kinds don't
    // climb — the mix policy handles them).
    private static readonly Level[] Ladder =
    {
        new("sbyte", sbyte.MinValue, sbyte.MaxValue, false),
        new("byte", byte.MinValue, byte.MaxValue, false),
        new("short", short.MinValue, short.MaxValue, false),
        new("ushort", ushort.MinValue, ushort.MaxValue, false),
        new("int", int.MinValue, int.MaxValue, false),
        new("uint", uint.MinValue, uint.MaxValue, false),
        new("long", long.MinValue, long.MaxValue, false),
        new("ulong", ulong.MinValue, ulong.MaxValue, false),
        new("int128", (BigInteger)Int128.MinValue, (BigInteger)Int128.MaxValue, false),
        new("uint128", (BigInteger)UInt128.MinValue, (BigInteger)UInt128.MaxValue, false),
        new("biginteger", BigInteger.Zero, BigInteger.Zero, true),
    };

    private static int LadderIndex(string kind)
    {
        for (int i = 0; i < Ladder.Length; i++)
            if (Ladder[i].Kind == kind) return i;
        return Ladder.Length - 1; // biginteger
    }

    // Signed-biased climb: an overflow widens along the SIGNED track (int → long → int128 → biginteger)
    // rather than into the matching-width unsigned kind — so int+int overflow → long (not uint).
    private static readonly string[] SignedClimb = { "int", "long", "int128", "biginteger" };

    private static BigInteger MaxMagnitude(string kind)
    {
        var r = Ladder[LadderIndex(kind)];
        return r.Unbounded ? BigInteger.Pow(2, 4096) : BigInteger.Max(BigInteger.Abs(r.Min), BigInteger.Abs(r.Max));
    }

    /// <summary>Narrow a BigInteger result: keep the floor kind (wider of the operands) when the value
    /// fits it; else widen along the signed track to the smallest signed kind that both exceeds the
    /// floor's range and holds the value.</summary>
    private static @this Narrow(BigInteger v, string floor)
    {
        var floorLevel = Ladder[LadderIndex(floor)];
        if (floorLevel.Fits(v)) return Mint(v, floor);

        BigInteger floorMag = MaxMagnitude(floor);
        foreach (var k in SignedClimb)
        {
            if (MaxMagnitude(k) <= floorMag) continue;
            if (Ladder[LadderIndex(k)].Fits(v)) return Mint(v, k);
        }
        return (@this)v; // BigInteger catch-all
    }

    /// <summary>Strict-width (Overflow.Throw): keep the floor kind; overflow throws.</summary>
    private static @this NarrowStrict(BigInteger v, string floor)
    {
        var r = Ladder[LadderIndex(floor)];
        if (r.Fits(v)) return floor == "biginteger" ? (@this)v : Mint(v, floor);
        throw new System.OverflowException(
            $"Arithmetic result {v} does not fit kind '{floor}' (overflow=throw / strict-width).");
    }

    // Place a BigInteger into a kind's exact CLR storage — the narrowing checked casts.
    private static @this Mint(BigInteger v, string kind) => kind switch
    {
        "sbyte" => (@this)(sbyte)v,
        "byte" => (@this)(byte)v,
        "short" => (@this)(short)v,
        "ushort" => (@this)(ushort)v,
        "int" => (@this)(int)v,
        "uint" => (@this)(uint)v,
        "long" => (@this)(long)v,
        "ulong" => (@this)(ulong)v,
        "int128" => (@this)(Int128)v,
        "uint128" => (@this)(UInt128)v,
        _ => (@this)v,
    };

    /// <summary>The wider (higher-ladder) of two integer kinds — the narrowing floor.</summary>
    private static string WiderInteger(string a, string b)
        => LadderIndex(a) >= LadderIndex(b) ? a : b;
}
