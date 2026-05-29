namespace app.type.code;

/// <summary>
/// Build-time kind hook for <c>code</c>. Reads the language from the
/// literal source without constructing the value.
/// </summary>
public sealed partial class @this
{
    public static string? Build(object? value)
    {
        if (value is not string raw) return null;
        if (string.IsNullOrEmpty(raw)) return null;
        if (raw.StartsWith('%') && raw.EndsWith('%')) return null;
        return DetectLanguage(raw);
    }
}
