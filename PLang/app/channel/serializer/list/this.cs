using app.channel.serializer;

namespace app.channel.serializer.list;

/// <summary>
/// Registry for serializers, allowing lookup by MIME type or file extension.
/// </summary>
[global::app.Attributes.PlangType("serializers")]
public sealed class @this
{
    private readonly Dictionary<string, ISerializer> _byType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISerializer> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private ISerializer _default;

    /// <summary>
    /// Construct with an actor Context — the bundled serializers born deserialized
    /// values on it, and the plang serializer carries a Context-bound PathJsonConverter
    /// so deserialized Goal/GoalCall objects land with Path fields fully wired. Every
    /// serializer registry belongs to an actor, and an actor always has a context.
    /// </summary>
    public @this(actor.context.@this context)
    {
        var json = new global::app.channel.serializer.Json(context);
        var text = new global::app.channel.serializer.Text(context);
        var plang = new global::app.channel.serializer.plang.@this(context);

        Register(json);
        Register(text);
        Register(plang);

        // Register alternative MIME types
        _byType["text/json"] = json;
        _byType["application/json; charset=utf-8"] = json;
        _byType["application/plang+json"] = plang;
        // text/html shares the JSON wire shape — global::app.channel.serializer.Json emits Value only.
        _byType["text/html"] = json;

        _default = json;
    }

    /// <summary>
    /// Registers a serializer.
    /// </summary>
    public void Register(ISerializer serializer)
    {
        _byType[serializer.Type] = serializer;
        _byExtension[serializer.Extension] = serializer;
    }

    /// <summary>
    /// Gets a serializer by mimetype, throwing <see cref="UnregisteredMimeType"/> when not
    /// registered. Use this when the contract is "the caller named a specific wire shape and
    /// expects routing to succeed" — in particular, Channel routing for outbound Data with an
    /// explicit MIME type. Counterpart of <see cref="GetByType"/> which returns null.
    /// </summary>
    public ISerializer GetByMimeType(string mimeType)
    {
        var s = GetByType(mimeType);
        if (s == null) throw new UnregisteredMimeType(mimeType);
        return s;
    }

    /// <summary>Select a serializer by mimetype — the collection's selection API
    /// (throws <see cref="UnregisteredMimeType"/> when absent, like
    /// <see cref="GetByMimeType"/>).</summary>
    public ISerializer this[string mimeType] => GetByMimeType(mimeType);

    /// <summary>
    /// Gets a serializer by MIME type. Strips charset suffix if present.
    /// </summary>
    public ISerializer? GetByType(string type)
    {
        // Strip charset if present
        var semicolon = type.IndexOf(';');
        if (semicolon > 0)
            type = type[..semicolon].Trim();

        return _byType.TryGetValue(type, out var serializer) ? serializer : null;
    }

    /// <summary>
    /// Gets a serializer by file extension. Walks multi-segment extensions
    /// from most-specific to least-specific so a registration for ".junit.xml"
    /// is preferred over a generic ".xml" registration, and absence of the
    /// multi-segment registration falls back to the trailing segment.
    /// </summary>
    public ISerializer? GetByExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return null;
        if (!extension.StartsWith('.')) extension = "." + extension;

        var probe = extension;
        while (true)
        {
            if (_byExtension.TryGetValue(probe, out var serializer)) return serializer;
            var nextDot = probe.IndexOf('.', 1);
            if (nextDot < 0) return null;
            probe = probe[nextDot..];
        }
    }

    /// <summary>
    /// Gets a serializer by MIME type or falls back to default.
    /// </summary>
    public ISerializer GetOrDefault(string? type)
    {
        if (string.IsNullOrEmpty(type))
            return _default;

        return GetByType(type) ?? _default;
    }

    /// <summary>
    /// Gets or sets the default serializer.
    /// </summary>
    public ISerializer Default
    {
        get => _default;
        set => _default = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the JSON serializer.
    /// </summary>
    public ISerializer Json => _byType["application/json"];

    /// <summary>
    /// Gets the text serializer.
    /// </summary>
    public ISerializer Text => _byType["text/plain"];

    /// <summary>
    /// The transport serializer (<c>application/plang</c>) — the self-describing Data
    /// container's own serializer, and the reference a captured <c>wire</c> slice holds to
    /// decode itself. A named door, so the channel receive path compares against it instead
    /// of type-checking <c>is plang.@this</c>.
    /// </summary>
    public ITransport Transport => (ITransport)_byType["application/plang"];

    /// <summary>
    /// Gets all registered MIME types.
    /// </summary>
    public IEnumerable<string> Types => _byType.Keys;

    /// <summary>
    /// Gets all registered file extensions.
    /// </summary>
    public IEnumerable<string> Extensions => _byExtension.Keys;

}
