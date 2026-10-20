namespace app.type.item.path.scheme;

/// <summary>
/// A path scheme ("file", "http", "https", …) — a kind of path, holding the factory that mints the
/// corresponding <see cref="global::app.type.item.path.@this"/> subclass. The built-in ones are kind classes
/// (<c>scheme/file</c>, <c>scheme/http</c>, <c>scheme/https</c>), made when path's kinds are first asked; an assembly
/// loaded via <c>code.load</c> adds its own. Per-App, so multi-App test harnesses add different schemes per App without bleed.
/// </summary>
public class @this : global::app.type.kind.@this<global::app.type.item.path.@this>
{
    private readonly Func<string, actor.context.@this, global::app.type.item.path.@this> _factory;

    /// <summary>The scheme <paramref name="scheme"/> of <paramref name="owner"/> (the app's path), whose paths
    /// <paramref name="factory"/> makes.</summary>
    public @this(string scheme, global::app.type.@this owner, Func<string, actor.context.@this, global::app.type.item.path.@this> factory)
        : base(scheme.ToLowerInvariant(), owner)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheme);
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>The path <paramref name="raw"/> names under this scheme.</summary>
    public global::app.type.item.path.@this Create(string raw, actor.context.@this context) => _factory(raw, context);

    /// <summary>
    /// Returns the scheme portion of <paramref name="raw"/> (everything
    /// before the first <c>://</c>), or empty when there is no scheme.
    /// </summary>
    public static string ParseScheme(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var idx = raw.IndexOf("://", StringComparison.Ordinal);
        if (idx <= 0) return "";
        // Validate the scheme part is alphanumeric/+/-/. per RFC 3986.
        for (int i = 0; i < idx; i++)
        {
            char c = raw[i];
            if (!(char.IsLetterOrDigit(c) || c == '+' || c == '-' || c == '.')) return "";
        }
        return raw[..idx];
    }
}

/// <summary>
/// A raw path names a scheme that is not one of the app's path kinds (<c>s3://</c>) — the program's
/// mistake, carried with its Error: key <c>SchemeNotRegistered</c> and how to fix it.
/// </summary>
public sealed class SchemeNotRegistered : global::app.error.AppException
{
    public string Scheme { get; }
    public SchemeNotRegistered(string scheme)
        : base(new global::app.error.@this($"No path scheme registered for '{scheme}'.", "SchemeNotRegistered", 400)
            { FixSuggestion = "Use a path plang reads: a bare path (/folder/file.txt), a file:// path, or an http(s):// url." })
    {
        Scheme = scheme;
    }
}
