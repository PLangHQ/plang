using System.Globalization;
using System.Numerics;

namespace app.type.number;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>number</c> owns how a numeric value is built. <paramref name="kind"/>
    /// picks the exact CLR precision across the full tower; a null kind derives it
    /// from the literal shape (string) or the value's own CLR type. Output is ALWAYS
    /// the born-native <see cref="@this"/> wrapper at that precision — a value built
    /// by its type IS a plang value. A .NET edge that needs the raw CLR numeric
    /// unwraps it with <c>.Clr&lt;T&gt;()</c>. Parsing is invariant-culture.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is null) return context.Ok(value);
        // Born-native: a value arrives as its wrapper (text "0.1", a number, …).
        // Unwrap to the raw backing so the string-parse / CLR-numeric paths below
        // see what they expect instead of ChangeType-ing a wrapper.
        if (value is global::app.type.item.@this iv) value = iv.Clr<object>();

        NumberKind? k = KindFromName(kind);
        if (k == null)
        {
            k = value is string s ? KindFromName(Build(s)) : ClrToKindSafe(value.GetType());
            if (k == null && value is string)
            {
                var parsed = Parse((string)value);
                if (parsed == null) return Fail(value, kind);
                return context.Ok(parsed);
            }
        }
        if (k == null) return Fail(value, kind);

        try
        {
            object raw = CoerceToKind(value, k.Value);
            return context.Ok(FromObject(raw));
        }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            return context.Error(new global::app.error.Error(
                $"Cannot read '{value}' as {kind ?? "number"}: {ex.Message}",
                "NumberConversionFailed", 400) { Exception = ex });
        }
    }

    private static global::app.data.@this Fail(object value, string? kind)
        => global::app.data.@this.FromError(new global::app.error.Error(
            $"Cannot convert {value.GetType().Name} to number.", "NumberConversionFailed", 400)
            { FixSuggestion = "Expected an integer or decimal literal (e.g. 42, 3.14)." });

    private static NumberKind? ClrToKindSafe(System.Type t)
    {
        try { return ClrToKind(t); } catch { return null; }
    }

    /// <summary>Produce the exact CLR numeric of <paramref name="k"/> from a string or numeric source.</summary>
    private static object CoerceToKind(object value, NumberKind k)
    {
        // Int128 / UInt128 / BigInteger / Half — System.Convert.ChangeType can't reach these.
        switch (k)
        {
            case NumberKind.BigInteger:
                return value is string bs ? BigInteger.Parse(bs, CultureInfo.InvariantCulture)
                                          : FromObject(value)!.AsBigInteger();
            case NumberKind.Int128:
                return value is string is128 ? Int128.Parse(is128, CultureInfo.InvariantCulture)
                                             : (Int128)FromObject(value)!.AsBigInteger();
            case NumberKind.UInt128:
                return value is string us128 ? UInt128.Parse(us128, CultureInfo.InvariantCulture)
                                             : (UInt128)FromObject(value)!.AsBigInteger();
            case NumberKind.Half:
                return value is string hs ? Half.Parse(hs, CultureInfo.InvariantCulture)
                                          : (Half)FromObject(value)!.AsDouble();
        }

        // Everything ChangeType supports: sbyte/byte/short/ushort/int/uint/long/ulong/float/double/decimal.
        // ChangeType parses strings and converts numerics (rounding for integer targets).
        var target = KindToClrType(k)!;
        return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
    }
}
