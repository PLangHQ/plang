namespace app.channels.serializers.serializer;

/// <summary>
/// Interface for serializing and deserializing objects in App.
/// Every method returns Data so parse/serialize failures surface as
/// Data.Error (Success=false) instead of throwing — callers stay on the
/// universal result-handling path and the legitimate-null case stays
/// distinguishable from the failure case.
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
    /// Serializes an object to a stream. Ok on success; Fail with the
    /// originating exception on serializer error.
    /// </summary>
    Task<data.@this> SerializeAsync(Stream stream, object? value, Type? type = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes from a stream. Data.Value carries the deserialized object;
    /// Data.Fail carries the parse error.
    /// </summary>
    Task<data.@this> DeserializeAsync(Stream stream, Type type, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generic stream deserialize. Data<T>.Value carries the typed result;
    /// Data<T>.FromError on parse failure.
    /// </summary>
    Task<data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronous serialize-to-string convenience. Data.Value carries the
    /// rendered text; Data.Fail on serializer error.
    /// </summary>
    data.@this<string> Serialize(object? value, Type? type = null);

    /// <summary>
    /// Synchronous string deserialize. Data.Value carries the deserialized
    /// object; Data.Fail on parse error.
    /// </summary>
    data.@this Deserialize(string data, Type type);

    /// <summary>
    /// Generic string deserialize. Data<T>.Value carries the typed result.
    /// </summary>
    data.@this<T> Deserialize<T>(string data);
}
