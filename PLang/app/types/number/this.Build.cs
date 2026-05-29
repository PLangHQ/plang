namespace app.types.number;

/// <summary>
/// Build-time kind hook for <c>number</c>. Discovered by the
/// <see cref="app.types.kinds.@this"/> dispatcher; called once by the
/// builder when stamping a literal whose declared type is <c>number</c>,
/// so the kind lands in the <c>.pr</c> alongside <c>type=number</c>.
///
/// <para>Rules (literal shape, not value inspection):
/// decimal point → <c>"decimal"</c>; exponent / NaN / Infinity →
/// <c>"double"</c>; bare integer fitting <c>int</c> → <c>"int"</c>; else
/// <c>"long"</c>. A non-string CLR primitive is read as its native kind
/// (<c>int</c> → <c>"int"</c>, …).</para>
/// </summary>
public sealed partial class @this
{
    public static string? Build(object? value)
    {
        switch (value)
        {
            case null: return null;
            case int: return "int";
            case long: return "long";
            case decimal: return "decimal";
            case float: return "double"; // float widens to double slot
            case double: return "double";
            case string s: return BuildFromString(s);
            default: return null;
        }
    }

    private static string? BuildFromString(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();
        if (s.StartsWith('%') && s.EndsWith('%')) return null; // variable reference

        bool hasDot = s.Contains('.');
        bool hasExp = s.Contains('e') || s.Contains('E');
        bool isSpecial = s.Equals("NaN", System.StringComparison.OrdinalIgnoreCase)
                      || s.EndsWith("Infinity", System.StringComparison.OrdinalIgnoreCase);

        if (hasExp || isSpecial)
        {
            // Confirm it actually parses as a double — "hello" contains 'e' but
            // isn't a number. The literal-shape rule applies to literals, not
            // free-form strings.
            if (double.TryParse(s, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
                return "double";
            return null;
        }
        if (hasDot)
        {
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out _))
                return "decimal";
            return null;
        }

        if (long.TryParse(s, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var l))
            return (l >= int.MinValue && l <= int.MaxValue) ? "int" : "long";

        if (decimal.TryParse(s, System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out _))
            return "decimal";

        return null;
    }
}
