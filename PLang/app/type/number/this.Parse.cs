namespace app.type.number;

/// <summary>
/// String → number parse path. Narrowest-fit: no decimal/exponent
/// → int → long; decimal point → decimal; exponent / NaN / Infinity → double.
///
/// <para><c>Resolve(string, context)</c> is the source-generator-recognized
/// factory — the catalog reads it via reflection to render <c>number</c> as
/// a scalar with shape <c>string</c>. The <c>context</c> parameter exists
/// for factory-signature consistency with other types' <c>Resolve</c>; number
/// never stores it.</para>
/// </summary>
public sealed partial class @this
{
    public static @this? Parse(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = s.Trim();

        bool hasDot = s.Contains('.');
        bool hasExp = s.Contains('e') || s.Contains('E');
        bool isSpecial = s.Equals("NaN", System.StringComparison.OrdinalIgnoreCase)
                      || s.EndsWith("Infinity", System.StringComparison.OrdinalIgnoreCase)
                      || s.Equals("-Infinity", System.StringComparison.OrdinalIgnoreCase);

        if (!hasDot && !hasExp && !isSpecial)
        {
            if (long.TryParse(s, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var l))
            {
                if (l >= int.MinValue && l <= int.MaxValue)
                    return (@this)((int)l);
                return (@this)(l);
            }
            // Past long — try decimal for very large integers.
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var bigDec))
                return (@this)(bigDec);
            return null;
        }

        if (hasDot && !hasExp && !isSpecial)
        {
            if (decimal.TryParse(s, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var dec))
                return (@this)(dec);
            // Fall through to double on decimal range overflow.
        }

        if (double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
            return (@this)(d);

        return null;
    }

    public static bool TryParse(string s, out @this? n)
    {
        n = Parse(s);
        return n != null;
    }

    /// <summary>
    /// Source-generator factory. Throws when the input isn't parseable as
    /// a number — the action-site contract is "this must be a number".
    /// <paramref name="context"/> is taken for signature uniformity and
    /// deliberately NOT stored on the resulting value.
    /// </summary>
    public static @this Resolve(string raw, global::app.actor.context.@this context)
    {
        if (string.IsNullOrEmpty(raw)) throw new System.FormatException("number.Resolve received empty input");
        var n = Parse(raw);
        if (n == null) throw new System.FormatException($"number.Resolve could not parse '{raw}' as a number");
        return n;
    }
}
