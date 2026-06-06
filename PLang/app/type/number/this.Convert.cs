using System.Globalization;
using System.Numerics;

namespace app.type.number;

public sealed partial class @this
{
    /// <summary>
    /// OBP: <c>number</c> owns how a numeric value is built. <paramref name="kind"/>
    /// picks the exact CLR precision across the full tower; a null kind derives it
    /// from the literal shape (string) or the value's own CLR type. Output is the
    /// exact CLR numeric (the alias target downstream expects), not a <see cref="@this"/>.
    /// Parsing is invariant-culture. This is also the registry's <c>number.Read</c>.
    /// </summary>
    public static global::app.data.@this Convert(object? value, string? kind,
        global::app.actor.context.@this context)
    {
        if (value is null) return global::app.data.@this.Ok(value);
        // Born-native: a value arrives as its wrapper (text "0.1", a number, …).
        // Unwrap to the raw backing so the string-parse / CLR-numeric paths below
        // see what they expect instead of ChangeType-ing a wrapper.
        if (value is global::app.type.item.@this iv) value = iv.ToRaw();

        NumberKind? k = KindFromName(kind);
        if (k == null)
        {
            k = value is string s ? KindFromName(Build(s)) : ClrToKindSafe(value.GetType());
            if (k == null && value is string)
            {
                // free-form numeric string with no declared kind — parse narrowest-fit.
                var parsed = Parse((string)value);
                return parsed == null
                    ? Fail(value, kind)
                    : global::app.data.@this.Ok(parsed.BoxedValue);
            }
        }
        if (k == null) return Fail(value, kind);

        try { return global::app.data.@this.Ok(CoerceToKind(value, k.Value)); }
        catch (System.Exception ex) when (ex is not (System.NullReferenceException or System.OutOfMemoryException or System.StackOverflowException))
        {
            return global::app.data.@this.FromError(new global::app.error.Error(
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
