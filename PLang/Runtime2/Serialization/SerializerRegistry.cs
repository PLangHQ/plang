namespace PLang.Runtime2.Serialization;

/// <summary>
/// Registry for serializers, allowing lookup by content type or file extension.
/// </summary>
public sealed class SerializerRegistry
{
    private readonly Dictionary<string, ISerializer> _byContentType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ISerializer> _byExtension = new(StringComparer.OrdinalIgnoreCase);
    private ISerializer _default;

    public SerializerRegistry()
    {
        var json = new JsonStreamSerializer();
        var text = new TextStreamSerializer();

        Register(json);
        Register(text);

        // Register alternative content types
        _byContentType["text/json"] = json;
        _byContentType["application/json; charset=utf-8"] = json;

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
        ISerializer serializer;
        if (!string.IsNullOrEmpty(options.ContentType))
            serializer = GetOrDefault(options.ContentType);
        else if (!string.IsNullOrEmpty(options.Extension))
            serializer = GetByExtension(options.Extension) ?? _default;
        else
            serializer = _default;

        if (options.Value is string str)
            return serializer.Deserialize<T>(str);

        if (options.Value is T typed)
            return typed;

        return default;
    }
}

/// <summary>
/// Options for deserialization — carries the raw content and metadata for serializer selection.
/// </summary>
public class DeserializeOptions
{
    public object? Value { get; init; }
    public string? Extension { get; init; }
    public string? ContentType { get; init; }
}
