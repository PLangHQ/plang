namespace PLang.Runtime2.Engine.Channels;

/// <summary>
/// Interface for serializing and deserializing objects in Runtime2.
/// Primary API is stream-based for efficiency.
/// </summary>
public interface ISerializer
{
    /// <summary>
    /// The content type this serializer handles (e.g., "application/json", "text/plain").
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// File extension associated with this format (e.g., ".json", ".txt").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Serializes an object to a stream.
    /// </summary>
    Task SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes an object from a stream.
    /// </summary>
    Task<object?> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes an object from a stream.
    /// </summary>
    Task<T?> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes an object to a string (convenience method).
    /// </summary>
    string Serialize(object? value, Type? type = null);

    /// <summary>
    /// Deserializes an object from a string (convenience method).
    /// </summary>
    object? Deserialize(string data, Type type);

    /// <summary>
    /// Deserializes an object from a string (convenience method).
    /// </summary>
    T? Deserialize<T>(string data);
}
