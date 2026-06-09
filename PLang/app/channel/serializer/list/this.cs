using app.channel.serializer;

namespace app.channel.serializer.list;

/// <summary>
/// Registry for serializers, allowing lookup by MIME type or file extension.
/// </summary>
public sealed class @this
{
    private readonly Dictionary<string, ISerializer> _byType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISerializer> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private ISerializer _default;

    public @this() : this(null) { }

    /// <summary>
    /// Construct with an actor Context — the bundled plang serializer carries
    /// a Context-bound PathJsonConverter so deserialized Goal/GoalCall objects
    /// land with Path fields fully wired. Per-Actor instances pass their
    /// Context here; the default ctor stays for tests and contexts where no
    /// Actor is in scope (Paths fall back to stubs).
    /// </summary>
    public @this(actor.context.@this? context)
    {
        var json = new global::app.channel.serializer.Json(context);
        var text = new global::app.channel.serializer.Text(jsonFallback: json);
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
    /// Gets all registered MIME types.
    /// </summary>
    public IEnumerable<string> Types => _byType.Keys;

    /// <summary>
    /// Gets all registered file extensions.
    /// </summary>
    public IEnumerable<string> Extensions => _byExtension.Keys;

    /// <summary>
    /// Deserializes content using the appropriate serializer, chosen by extension or MIME type.
    /// </summary>
    public data.@this<T> Deserialize<T>(DeserializeOptions options) where T : global::app.type.item.@this
    {
        var serializer = ResolveSerializer(new ResolveOptions { Type = options.Type, Extension = options.Extension });

        if (options.Value is string str)
            return serializer.Deserialize<T>(str);

        if (options.Value is T typed)
            return data.@this<T>.Ok(typed);

        return data.@this<T>.Ok(default!);
    }

    /// <summary>
    /// Serializes data to a stream using the appropriate serializer.
    /// </summary>
    public Task<data.@this> SerializeAsync(SerializeOptions options)
    {
        var serializer = ResolveForWrite(options.Type, options.Extension, options.Data);
        return serializer.SerializeAsync(options.Stream, options.Data, cancellationToken: options.CancellationToken);
    }

    /// <summary>
    /// Serializer selection for a write, content-aware on the fallback. An
    /// explicit MIME or a registered extension wins; otherwise the value's shape
    /// decides — a <c>string</c> writes as text, anything else (a structured
    /// object) rides the Data wire (<c>application/plang</c>), which renders
    /// domain types via their per-type serializers. The plain <c>application/json</c>
    /// STJ path can't render a domain object like a snapshot, so it is NOT the
    /// structured fallback. (<c>byte[]</c> never reaches here — path.Save writes
    /// raw bytes before the channel.)
    /// </summary>
    private ISerializer ResolveForWrite(string? type, string? extension, data.@this data)
    {
        if (!string.IsNullOrEmpty(type)) return GetOrDefault(type);
        if (!string.IsNullOrEmpty(extension) && GetByExtension(extension) is { } byExt) return byExt;
        if (data.Materialize() is string) return GetByType("text/plain") ?? _default;
        return GetByType("application/plang") ?? _default;
    }

    /// <summary>
    /// Deserializes data from a stream using the appropriate serializer.
    /// </summary>
    public Task<data.@this<T>> DeserializeAsync<T>(DeserializeOptions options) where T : global::app.type.item.@this
    {
        if (options.Stream == null)
            throw new ArgumentException("Stream is required for async deserialization", nameof(options));

        var serializer = ResolveSerializer(new ResolveOptions { Type = options.Type, Extension = options.Extension });
        return serializer.DeserializeAsync<T>(options.Stream, options.CancellationToken);
    }

    private ISerializer ResolveSerializer(ResolveOptions options)
    {
        if (!string.IsNullOrEmpty(options.Type))
            return GetOrDefault(options.Type);
        if (!string.IsNullOrEmpty(options.Extension))
            return GetByExtension(options.Extension) ?? _default;
        return _default;
    }
}

/// <summary>
/// Options for serialization — carries stream, data, and metadata for serializer selection.
/// </summary>
public class SerializeOptions
{
    public Stream Stream { get; init; } = null!;
    public data.@this Data { get; init; } = global::app.data.@this.Ok();
    public string? Type { get; init; }
    public string? Extension { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Options for deserialization — carries the raw content and metadata for serializer selection.
/// </summary>
public class DeserializeOptions
{
    public object? Value { get; init; }
    public Stream? Stream { get; init; }
    public string? Extension { get; init; }
    public string? Type { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// Options for serializer resolution — names the MIME type or file extension
/// to drive registry lookup.
/// </summary>
public class ResolveOptions
{
    public string? Type { get; init; }
    public string? Extension { get; init; }
}
