namespace PLang.Runtime2.IO;

/// <summary>
/// Represents data that can be sent/received through a channel.
/// </summary>
public sealed class ChannelData
{
    public object? Value { get; }
    public string? ContentType { get; }
    public IDictionary<string, string>? Metadata { get; }
    public DateTime Timestamp { get; }

    public ChannelData(object? value, string? contentType = null, IDictionary<string, string>? metadata = null)
    {
        Value = value;
        ContentType = contentType;
        Metadata = metadata;
        Timestamp = DateTime.UtcNow;
    }

    /// <summary>
    /// Creates a ChannelData with JSON content type.
    /// </summary>
    public static ChannelData Json(object? value) => new(value, "application/json");

    /// <summary>
    /// Creates a ChannelData with text content type.
    /// </summary>
    public static ChannelData Text(string? value) => new(value, "text/plain");

    /// <summary>
    /// Creates a ChannelData with binary content type.
    /// </summary>
    public static ChannelData Binary(byte[]? value) => new(value, "application/octet-stream");

    /// <summary>
    /// Gets the value as a specific type.
    /// </summary>
    public T? GetValue<T>()
    {
        if (Value is T typed)
            return typed;
        return default;
    }

    /// <summary>
    /// Checks if the data is empty/null.
    /// </summary>
    public bool IsEmpty => Value == null ||
        (Value is string s && string.IsNullOrEmpty(s)) ||
        (Value is byte[] b && b.Length == 0);

    public override string ToString() => Value?.ToString() ?? "(null)";
}
