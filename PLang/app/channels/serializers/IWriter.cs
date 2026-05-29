namespace app.channels.serializers;

/// <summary>
/// Format-encoder protocol consumed by the wire pipeline once
/// <see cref="app.data.@this"/> has been normalized.
///
/// <para>The pipeline is: <c>Normalize()</c> walks the in-memory value into a
/// uniform tree of <c>primitive | byte[] | Data | List&lt;&gt;</c>; an
/// <see cref="IWriter"/> implementation then walks the tree and emits bytes
/// in its target format. JSON (<see cref="json.Writer"/>) is the first impl;
/// protobuf / MsgPack / CBOR ship later as siblings without touching Normalize
/// or any domain type.</para>
///
/// <para>The surface is deliberately minimal: leaf primitives, an array
/// bracket (count optional — JSON ignores, protobuf may use), and a Data
/// record bracket (the canonical <c>{name, type, value, properties, signature}</c>
/// envelope, which only the writer knows how to lay out per format).</para>
/// </summary>
public interface IWriter
{
    /// <summary>
    /// Short format token — <c>"json"</c>, <c>"plang"</c>, <c>"text"</c>,
    /// <c>"protobuf"</c>, … Used as the dispatch key when looking up a
    /// per-(type, format) renderer for a <see cref="app.data.TypedValueNode"/>.
    /// Each <see cref="IWriter"/> impl returns its own constant token; the
    /// channel-layer serializer registry maps mime → writer instance, but
    /// type-renderer dispatch keys off this short token, never the mime.
    /// </summary>
    string Format { get; }

    void Null();
    void Bool(bool value);
    void Int(int value);
    void Long(long value);
    void Float(float value);
    void Double(double value);
    void String(string value);
    void DateTime(System.DateTime value);
    void DateTimeOffset(System.DateTimeOffset value);
    void TimeSpan(System.TimeSpan value);
    void Guid(System.Guid value);
    void Enum(System.Enum value);
    void Decimal(decimal value);
    void Bytes(byte[] value);

    /// <summary>
    /// Begin an array bracket. <paramref name="count"/> is -1 when the writer
    /// cannot determine the length up front; format encoders that need a
    /// known length (protobuf packed repeated, length-prefixed binary
    /// formats) may have to buffer in that case.
    /// </summary>
    void BeginArray(int count);
    void EndArray();

    /// <summary>
    /// Begin a Data record bracket. The writer emits the canonical
    /// <c>{name, type, value, properties, signature}</c> envelope shape for
    /// its format and accepts the payload from subsequent calls. Implementations
    /// own the layout; the caller only invokes value-emission methods between
    /// <see cref="BeginRecord"/> and <see cref="EndRecord"/>.
    /// </summary>
    void BeginRecord(app.data.@this record);
    void EndRecord();

    /// <summary>
    /// Write the value slot of the current record. Dispatched by Normalize
    /// based on the runtime type of the normalized value.
    /// </summary>
    void Value(object? normalized);
}
