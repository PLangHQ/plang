using App.Channels.Serializers.Serializer;

namespace App.Channels.Serializers;

/// <summary>
/// Registry for serializers, allowing lookup by content type or file extension.
/// </summary>
public sealed class @this
{
    private readonly Dictionary<string, ISerializer> _byContentType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISerializer> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private ISerializer _default;

    public @this()
    {
        var json = new global::App.Channels.Serializers.Serializer.Json();
        var text = new global::App.Channels.Serializers.Serializer.Text(jsonFallback: json);
        var plang = new global::App.Channels.Serializers.Serializer.Plang.@this();
        var plangData = new global::App.Channels.Serializers.Serializer.Plang.Data();

        Register(json);
        Register(text);
        Register(plang);
        Register(plangData);

        // Register alternative content types
        _byContentType["text/json"] = json;
        _byContentType["application/json; charset=utf-8"] = json;
        _byContentType["application/plang+json"] = plang;
        // text/html shares the JSON wire shape — global::App.Channels.Serializers.Serializer.Json emits Value only.
        _byContentType["text/html"] = json;

        _default = json;
    }

    /// <summary>
    /// Registers a serializer.
    /// </summary>
    public void Register(ISerializer serializer)
    {
        _byContentType[serializer.ContentType] = serializer;
        _byExtension[serializer.FileExtension] = serializer;
    }

    /// <summary>
    /// Gets a serializer by mimetype, throwing <see cref="UnregisteredMimeType"/> when not
    /// registered. Use this when the contract is "the caller named a specific wire shape and
    /// expects routing to succeed" — in particular, Channel routing for outbound Data with an
    /// explicit content type. Counterpart of <see cref="GetByContentType"/> which returns null.
    /// </summary>
    public ISerializer GetByMimeType(string mimeType)
    {
        var s = GetByContentType(mimeType);
        if (s == null) throw new UnregisteredMimeType(mimeType);
        return s;
    }

    /// <summary>
    /// Gets a serializer by content type.
    /// </summary>
    public ISerializer? GetByContentType(string contentType)
    {
        // Strip charset if present
        var semicolon = contentType.IndexOf(';');
        if (semicolon > 0)
            contentType = contentType[..semicolon].Trim();

        return _byContentType.TryGetValue(contentType, out var serializer) ? serializer : null;
    }

    /// <summary>
    /// Gets a serializer by file extension.
    /// </summary>
    public ISerializer? GetByExtension(string extension)
    {
        if (!extension.StartsWith('.'))
            extension = "." + extension;

        return _byExtension.TryGetValue(extension, out var serializer) ? serializer : null;
    }

    /// <summary>
    /// Gets a serializer by content type or falls back to default.
    /// </summary>
    public ISerializer GetOrDefault(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return _default;

        return GetByContentType(contentType) ?? _default;
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
    public ISerializer Json => _byContentType["application/json"];

    /// <summary>
    /// Gets the text serializer.
    /// </summary>
    public ISerializer Text => _byContentType["text/plain"];

    /// <summary>
    /// Gets all registered content types.
    /// </summary>
    public IEnumerable<string> ContentTypes => _byContentType.Keys;

    /// <summary>
    /// Gets all registered file extensions.
    /// </summary>
    public IEnumerable<string> Extensions => _byExtension.Keys;

    /// <summary>
    /// Deserializes content using the appropriate serializer, chosen by extension or contentType.
    /// </summary>
    public T? Deserialize<T>(DeserializeOptions options)
    {
        var serializer = ResolveSerializer(options.ContentType, options.Extension);

        if (options.Value is string str)
            return serializer.Deserialize<T>(str);

        if (options.Value is T typed)
            return typed;

        return default;
    }

    /// <summary>
    /// Serializes data to a stream using the appropriate serializer.
    /// </summary>
    public Task SerializeAsync(SerializeOptions options)
    {
        var serializer = ResolveSerializer(options.ContentType, options.Extension);
        return serializer.SerializeAsync(options.Stream, options.Data, cancellationToken: options.CancellationToken);
    }

    /// <summary>
    /// Deserializes data from a stream using the appropriate serializer.
    /// </summary>
    public Task<T?> DeserializeAsync<T>(DeserializeOptions options)
    {
        if (options.Stream == null)
            throw new ArgumentException("Stream is required for async deserialization", nameof(options));

        var serializer = ResolveSerializer(options.ContentType, options.Extension);
        return serializer.DeserializeAsync<T>(options.Stream, options.CancellationToken);
    }

    private ISerializer ResolveSerializer(string? contentType, string? extension)
    {
        if (!string.IsNullOrEmpty(contentType))
            return GetOrDefault(contentType);
        if (!string.IsNullOrEmpty(extension))
            return GetByExtension(extension) ?? _default;
        return _default;
    }
}

/// <summary>
/// Options for serialization — carries stream, data, and metadata for serializer selection.
/// </summary>
public class SerializeOptions
{
    public Stream Stream { get; init; } = null!;
    public object? Data { get; init; }
    public string? ContentType { get; init; }
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
    public string? ContentType { get; init; }
    public CancellationToken CancellationToken { get; init; }
}
