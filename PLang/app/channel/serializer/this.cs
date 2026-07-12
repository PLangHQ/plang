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
    /// Serializes a Data to a stream. Ok on success; Fail with the originating
    /// exception on serializer error. <paramref name="view"/> selects Out (transport)
    /// vs Store (local persistence). String I/O is NOT the serializer's concern —
    /// a store that persists text (sqlite TEXT) owns its own string↔stream bridge.
    /// </summary>
    Task<data.@this> SerializeAsync(Stream stream, data.@this data, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deserializes a Data from a stream. Data.Value carries the result; Data.Fail
    /// the parse error. The non-generic form is the transport-receive boundary (an
    /// arbitrary incoming message); a caller that knows the type uses the generic.
    /// </summary>
    Task<data.@this> DeserializeAsync(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generic stream deserialize — the value is forced to its plang item type T
    /// (<c>Data&lt;T&gt;</c>), never a raw string. Data&lt;T&gt;.FromError on parse failure.
    /// </summary>
    Task<data.@this<T>> DeserializeAsync<T>(Stream stream, global::app.View view = global::app.View.Out, CancellationToken cancellationToken = default) where T : global::app.type.item.@this, global::app.type.item.ICreate<T>;

    /// <summary>
    /// Reads a <c>source</c>'s undecoded bytes (from a <c>.pr</c> or channel content) into
    /// its plang type. THIS serializer knows the encoding its bytes are in, so it makes the
    /// matching <see cref="IReader"/> over them; the type then pulls itself off that reader
    /// (<c>App.Type.Reader(name, kind).Read</c>) — format-agnostic. The serializer navigates
    /// the source for its raw + declared type; the read-side counterpart of the write
    /// renderer (serializer owns the format, the type owns the value).
    /// </summary>
    global::app.type.item.@this Read(global::app.type.item.source source, global::app.type.reader.ReadContext ctx);

    /// <summary>
    /// True when <paramref name="writer"/> encodes in THIS serializer's own format — a raw
    /// <c>wire</c> slice this serializer captured rides VERBATIM into it (byte-identical relay).
    /// Any other writer is a different format, so writing the slice there is a USE: the wire
    /// materializes and the value writes itself (a text writer gets bare content, not the quoted
    /// document slice). Default false — a plain-content serializer round-trips no foreign slice;
    /// the transport overrides for its own json/plang writers. The wire asks its own reader this,
    /// so no format name lives on the wire (a bson transport answers for bson).
    /// </summary>
    bool Owns(global::app.channel.serializer.IWriter writer) => false;
}
