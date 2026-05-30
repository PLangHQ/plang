namespace app.type.text;

/// <summary>
/// Build-time kind hook for <c>text</c>. Discovered by
/// <see cref="app.type.kind.@this"/>. Pulls the file extension out of a
/// literal value as the kind (no dot, lowercased), exactly as
/// <c>image.Build</c> does for binary-backed types.
///
/// <para>Inputs:
///   <c>"readme.md"</c> → <c>"md"</c>;
///   <c>"page.HTML?v=1"</c> → <c>"html"</c>;
///   <c>"../report.md"</c> → <c>"md"</c>;
///   <c>"plain"</c> / <c>"%var%"</c> / <c>null</c> → <c>null</c>.</para>
/// </summary>
public sealed partial class @this
{
    public static string? Build(object? value)
    {
        if (value is null) return null;
        if (value is not string raw) return null;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (raw.StartsWith('%') && raw.EndsWith('%')) return null;

        var dotIdx = raw.LastIndexOf('.');
        if (dotIdx < 0 || dotIdx >= raw.Length - 1) return null;
        var ext = raw[(dotIdx + 1)..].ToLowerInvariant();
        // Strip trailing query/fragment for URL-shaped strings (page.html?v=1).
        var qIdx = ext.IndexOfAny(new[] { '?', '#' });
        if (qIdx > 0) ext = ext[..qIdx];
        return ext.Length > 0 ? ext : null;
    }
}
