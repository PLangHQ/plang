namespace app.type.item.path.scheme;

/// <summary>
/// A path scheme ("file", "http", "https", …) — a kind of path, holding the factory that mints the
/// corresponding <see cref="global::app.type.item.path.@this"/> subclass. Added to the app's kinds
/// at App construction (built-in file/http/https); an assembly loaded via <c>code.load</c> adds its
/// own. Per-App, so multi-App test harnesses add different schemes per App without bleed.
/// </summary>
public sealed class @this : global::app.type.kind.@this
{
    private readonly Func<string, actor.context.@this, global::app.type.item.path.@this> _factory;

    public @this(string scheme, Func<string, actor.context.@this, global::app.type.item.path.@this> factory)
        : base(scheme.ToLowerInvariant())
    {
        ArgumentException.ThrowIfNullOrEmpty(scheme);
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>A scheme is a kind of path.</summary>
    protected internal override string Owner => "path";

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
        : base(new global::app.error.Error($"No path scheme registered for '{scheme}'.", "SchemeNotRegistered", 400)
            { FixSuggestion = $"Add a path kind for scheme '{scheme}' (app.Type.Kind.Add(new path.scheme.@this(…))), or use a bare/file:// path." })
    {
        Scheme = scheme;
    }
}
