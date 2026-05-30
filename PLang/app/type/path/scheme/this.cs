using System.Collections.Concurrent;

namespace app.type.path.scheme;

/// <summary>
/// Per-App scheme registry. Maps scheme name ("file", "http", "https", …) to
/// a factory that mints the corresponding <see cref="Path"/> subclass.
/// </summary>
/// <remarks>
/// <para>
/// Registry is per-App, not static — multi-App test harnesses register
/// different schemes per App without bleed. Internal store is a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> so reads are lock-free
/// and registrations are atomic.
/// </para>
/// <para>
/// Bare paths (no <c>scheme://</c> prefix) default to the <c>file</c>
/// factory. Unknown schemes throw <see cref="SchemeNotRegistered"/>;
/// callers (PLang type-mapper) translate it into <c>data.@this.Fail</c>.
/// </para>
/// </remarks>
public sealed class @this
{
    private readonly ConcurrentDictionary<string, Func<string, actor.context.@this, global::app.type.path.@this>> _factories
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers (or replaces) the factory for <paramref name="scheme"/>.
    /// Scheme names are case-insensitive ("HTTP" and "http" collapse).
    /// </summary>
    public void Register(string scheme, Func<string, actor.context.@this, global::app.type.path.@this> factory)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheme);
        ArgumentNullException.ThrowIfNull(factory);
        _factories[scheme] = factory;
    }

    /// <summary>
    /// True when <paramref name="scheme"/> has a registered factory.
    /// </summary>
    public bool IsRegistered(string scheme) => _factories.ContainsKey(scheme);

    /// <summary>
    /// Constructs a Path from a raw string. Bare paths route to the
    /// <c>file</c> factory. Unknown schemes throw <see cref="SchemeNotRegistered"/>.
    /// </summary>
    public global::app.type.path.@this From(string raw, actor.context.@this context)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(context);

        var scheme = ParseScheme(raw);
        if (scheme.Length == 0)
        {
            if (_factories.TryGetValue("file", out var fileFactory))
                return fileFactory(raw, context);
            throw new SchemeNotRegistered("file");
        }
        if (_factories.TryGetValue(scheme, out var factory))
            return factory(raw, context);
        throw new SchemeNotRegistered(scheme);
    }

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
/// Thrown by <see cref="@this.From"/> when no factory is registered for the
/// requested scheme. The PLang type-mapper catches it and shapes it as
/// <c>data.@this.Fail</c>.
/// </summary>
public sealed class SchemeNotRegistered : Exception
{
    public string Scheme { get; }
    public SchemeNotRegistered(string scheme)
        : base($"No path scheme registered for '{scheme}'.")
    {
        Scheme = scheme;
    }
}
