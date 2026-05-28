using System.Text.Json;

namespace app.channels.serializers;

/// <summary>
/// JSON implementation of <see cref="IWriter"/> — wraps a
/// <see cref="Utf8JsonWriter"/>. Emits the canonical Data wire envelope
/// <c>{name, type, value, properties, signature}</c> for records, native JSON
/// tokens for primitives, JSON arrays for lists.
///
/// <para>The writer never reflects: Normalize has already decomposed any C#
/// object into a tree of primitives / byte[] / Data / List, so writing is a
/// pure dispatch on the runtime type of each visited value.</para>
/// </summary>
public sealed class JsonWriter : IWriter
{
    private readonly Utf8JsonWriter _writer;
    private readonly JsonSerializerOptions _options;

    public JsonWriter(Utf8JsonWriter writer, JsonSerializerOptions? options = null)
    {
        _writer = writer;
        _options = options ?? new JsonSerializerOptions();
    }

    public void Null() => _writer.WriteNullValue();
    public void Bool(bool value) => _writer.WriteBooleanValue(value);
    public void Int(int value) => _writer.WriteNumberValue(value);
    public void Long(long value) => _writer.WriteNumberValue(value);
    public void Double(double value) => _writer.WriteNumberValue(value);
    public void String(string value) => _writer.WriteStringValue(value);
    public void DateTime(System.DateTime value) => _writer.WriteStringValue(value);
    public void Decimal(decimal value) => _writer.WriteNumberValue(value);
    public void Bytes(byte[] value) => _writer.WriteBase64StringValue(value);

    public void BeginArray(int count) => _writer.WriteStartArray();
    public void EndArray() => _writer.WriteEndArray();

    /// <summary>
    /// Opens the canonical Data envelope. <c>name</c> is always present;
    /// <c>type</c> is written only when non-null; <c>value</c> is opened with
    /// the property name and left for the caller's value-emission calls;
    /// <c>properties</c> and <c>signature</c> are closed out by
    /// <see cref="EndRecord"/>.
    /// </summary>
    public void BeginRecord(app.data.@this record)
    {
        _writer.WriteStartObject();
        _writer.WriteString("name", record.Name);

        var typeVal = record.Type?.Value;
        if (typeVal != null)
            _writer.WriteString("type", typeVal);

        _writer.WritePropertyName("value");
    }

    /// <summary>
    /// Closes the canonical Data envelope, emitting the <c>properties</c>
    /// sidecar (if non-empty) and the <c>signature</c> (if present). The
    /// caller is responsible for invoking <see cref="Value"/> (or an
    /// equivalent leaf method) between Begin and End so the value slot is
    /// populated.
    /// </summary>
    public void EndRecord(app.data.@this record)
    {
        if (record.Properties.Count > 0)
        {
            _writer.WritePropertyName("properties");
            _writer.WriteStartObject();
            foreach (var kvp in record.Properties)
            {
                _writer.WritePropertyName(kvp.Key);
                JsonSerializer.Serialize(_writer, kvp.Value, _options);
            }
            _writer.WriteEndObject();
        }

        if (record.Signature != null)
        {
            _writer.WritePropertyName("signature");
            JsonSerializer.Serialize(_writer, record.Signature, _options);
        }

        _writer.WriteEndObject();
    }

    /// <summary>
    /// True when every element of <paramref name="list"/> is a <see cref="app.data.@this"/>
    /// with a non-empty <c>Name</c> — i.e. the list represents a domain object's
    /// property bag (Normalize's output for a non-list non-primitive). Returns
    /// the list itself (cast as IEnumerable of Data) via <paramref name="dataList"/>.
    /// </summary>
    private static bool IsNamedDataBag(System.Collections.IEnumerable list, out System.Collections.IEnumerable? dataList)
    {
        dataList = list;
        bool any = false;
        foreach (var item in list)
        {
            any = true;
            if (item is not app.data.@this d) return false;
            if (string.IsNullOrEmpty(d.Name)) return false;
        }
        return any;
    }

    void IWriter.EndRecord() => throw new System.InvalidOperationException(
        "JsonWriter.EndRecord requires the Data record reference — call EndRecord(Data) instead.");

    /// <summary>
    /// Writes a normalized value. The value must already be one of:
    /// null, primitive (string, int, long, double, float, bool, DateTime, decimal),
    /// byte[], <see cref="app.data.@this"/>, or <see cref="System.Collections.IEnumerable"/>
    /// of normalized values. Normalize is the producer; the writer is the consumer.
    /// </summary>
    public void Value(object? normalized)
    {
        switch (normalized)
        {
            case null: Null(); return;
            case bool b: Bool(b); return;
            case int i: Int(i); return;
            case long l: Long(l); return;
            case double d: Double(d); return;
            case float f: Double(f); return;
            case decimal dec: Decimal(dec); return;
            case string s: String(s); return;
            case System.DateTime dt: DateTime(dt); return;
            case byte[] bytes: Bytes(bytes); return;
            case app.data.@this nested:
                BeginRecord(nested);
                Value(nested.Value);
                EndRecord(nested);
                return;
            case System.Collections.IEnumerable list:
                if (IsNamedDataBag(list, out var nameKey))
                {
                    // Named List<Data> → JSON object. The property-bag form
                    // produced by Normalize on a domain object reads naturally
                    // as { name1: value1, name2: value2 } rather than an array
                    // of records.
                    _writer.WriteStartObject();
                    foreach (app.data.@this child in (System.Collections.IEnumerable)nameKey!)
                    {
                        _writer.WritePropertyName(child.Name);
                        Value(child.Value);
                    }
                    _writer.WriteEndObject();
                    return;
                }
                BeginArray(-1);
                foreach (var item in list) Value(item);
                EndArray();
                return;
            default:
                // Fallback: hand the unexpected object to STJ. Reaching here means
                // Normalize missed a case — surface a typed error in the future.
                JsonSerializer.Serialize(_writer, normalized, _options);
                return;
        }
    }
}
