namespace app.type.path;

/// <summary>
/// Build-time kind hook for <see cref="@this"/>. Reads the scheme out of a raw
/// path string without constructing the value — the registry's
/// <c>BuildKind(typeof(path), raw)</c> calls this when stamping a parameter
/// whose declared type is <c>path</c>.
///
/// Returns:
///   <c>null</c> for non-strings, empty input, or a <c>%var%</c> reference
///   (kind is decided at runtime);
///   <c>"http"</c> for both <c>http://</c> and <c>https://</c> (one family);
///   <c>"file"</c> otherwise — the implicit default for bare paths.
/// </summary>
public abstract partial class @this
{
    public static string? Build(object? value)
    {
        if (value is not string raw) return null;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (raw.StartsWith('%') && raw.EndsWith('%')) return null;

        var trimmed = raw.TrimStart();
        if (trimmed.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase)
         || trimmed.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase))
            return "http";

        var colonIdx = trimmed.IndexOf(':');
        if (colonIdx > 0)
        {
            var scheme = trimmed[..colonIdx].ToLowerInvariant();
            // A drive letter (Windows "C:") is not a URI scheme — keep it as file.
            if (scheme.Length > 1) return scheme;
        }

        return "file";
    }
}
