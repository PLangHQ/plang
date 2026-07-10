namespace app.type.item.image;

/// <summary>
/// Build-time kind hook for <c>image</c>. Discovered by
/// <see cref="app.type.kind.Hooks"/>. Reads the kind (extension, no dot,
/// lowercased) from a literal value without constructing the image.
///
/// Inputs:
///   <c>"photo.jpg"</c> → <c>"jpg"</c>;
///   <c>"a.PNG"</c> → <c>"png"</c>;
///   <c>"data:image/gif;base64,..."</c> → <c>"gif"</c>;
///   anything else / null / %var% → null.
/// </summary>
public sealed partial class @this
{
    public static string? Build(object? value)
    {
        if (value is not string raw) return null;
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (raw.StartsWith('%') && raw.EndsWith('%')) return null;

        if (raw.StartsWith("data:image/", System.StringComparison.OrdinalIgnoreCase))
        {
            var slashIdx = raw.IndexOf('/');
            var semiIdx = raw.IndexOf(';', slashIdx);
            if (slashIdx >= 0 && semiIdx > slashIdx)
                return raw[(slashIdx + 1)..semiIdx].ToLowerInvariant();
            return null;
        }

        var dotIdx = raw.LastIndexOf('.');
        if (dotIdx < 0 || dotIdx >= raw.Length - 1) return null;
        var ext = raw[(dotIdx + 1)..].ToLowerInvariant();
        // Strip trailing query/fragment for URLs (a.jpg?v=1).
        var qIdx = ext.IndexOfAny(new[] { '?', '#' });
        if (qIdx > 0) ext = ext[..qIdx];
        return ext.Length > 0 ? ext : null;
    }
}
