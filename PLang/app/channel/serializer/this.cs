namespace app.channel.serializer;

/// <summary>
/// Interface for serializing and deserializing Data in App.
/// Input is always Data — the wrapper carries name, value, type, and signature;
/// the MIME's identity decides what gets emitted (just the value for JSON/text,
/// the full wrapper for application/plang). Every method returns Data so
/// parse/serialize failures surface as Data.Error (Success=false) instead of
/// throwing — callers stay on the universal result-handling path and the
/// legitimate-null case stays distinguishable from the failure case.
/// </summary>
public interface ISerializer
{
    /// <summary>
    /// MIME type this serializer handles (e.g., "application/json", "text/plain").
    /// </summary>
    string Type { get; }

    /// <summary>
    /// File extension associated with this format (e.g., ".json", ".txt").
    /// </summary>
    string Extension { get; }

    /// <summary>
    /// Serializes a Data to a stream. Ok on success; Fail with the
    /// originating exception on serializer error.
    /// </summary>
    Task<data.@this> SerializeAsync(Stream stream, data.@this data, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes from a stream. Data.Value carries the deserialized object;
    /// Data.Fail carries the parse error.
    /// </summary>
    Task<data.@this> DeserializeAsync(Stream stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generic stream deserialize. Data&lt;T&gt;.Value carries the typed result;
    /// Data&lt;T&gt;.FromError on parse failure.
    /// </summary>
    Task<data.@this<T>> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : global::app.type.item.@this;

    /// <summary>
    /// Synchronous serialize-to-string convenience. Data.Value carries the
    /// rendered text; Data.Fail on serializer error.
    /// </summary>
    data.@this<global::app.type.text.@this> Serialize(data.@this data);

    /// <summary>
    /// Synchronous string deserialize. Data.Value carries the deserialized
    /// object; Data.Fail on parse error. For a typed view, use Deserialize&lt;T&gt;
    /// or call .As&lt;T&gt;() on the result.
    /// </summary>
    data.@this Deserialize(string s);

    /// <summary>
    /// Generic string deserialize. Data&lt;T&gt;.Value carries the typed result.
    /// </summary>
    data.@this<T> Deserialize<T>(string s) where T : global::app.type.item.@this;
}
