using System.Text.Json;
using app.type.format;

namespace app.type.item.kind.json;

/// <summary>
/// JSON implementation of <see cref="IWriter"/> — wraps a
/// <see cref="Utf8JsonWriter"/>: native JSON tokens for primitives, JSON arrays
/// and objects for structure.
///
/// <para>The writer never reflects: each value (a Data's record too) writes itself
/// through its own <c>Output</c>, and <see cref="Value"/> takes only the C# values of
/// the tree contract (C# primitives and byte[]).</para>
/// </summary>
public sealed class Writer : IWriter
{
    private readonly Utf8JsonWriter _writer;
    private readonly global::app.type.kind.@this _binary;   // the kind of bytes handed as a C# byte[]: plain bytes

    /// <summary>A json writer over <paramref name="writer"/>, its kind item's <c>json</c> among
    /// <paramref name="types"/>. Bare json shows a Data's value only (the record door writes nothing).</summary>
    public Writer(Utf8JsonWriter writer, global::app.type.list.@this types)
    {
        _writer = writer;
        Kind = types["item"].kind["json"]!;
        _binary = types[typeof(global::app.type.item.binary.@this)].kind;
    }

    public global::app.type.kind.@this Kind { get; }

    public void Null() => _writer.WriteNullValue();
    public void Bool(bool value) => _writer.WriteBooleanValue(value);
    public void Int(int value) => _writer.WriteNumberValue(value);
    public void Long(long value) => _writer.WriteNumberValue(value);
    public void Float(float value) => _writer.WriteNumberValue(value);
    public void Double(double value) => _writer.WriteNumberValue(value);
    public void String(string value) => _writer.WriteStringValue(value);
    // Verbatim — raw json (object/number) rides inline, unquoted.
    public void Raw(string value) => _writer.WriteRawValue(value);
    public void DateTime(System.DateTime value) => _writer.WriteStringValue(value);
    public void DateTimeOffset(System.DateTimeOffset value) => _writer.WriteStringValue(value);
    public void TimeSpan(System.TimeSpan value) => _writer.WriteStringValue(value.ToString("c"));
    public void Guid(System.Guid value) => _writer.WriteStringValue(value);
    public void Enum(System.Enum value) => _writer.WriteStringValue(value.ToString());
    public void Decimal(decimal value) => _writer.WriteNumberValue(value);
    // Bytes of a kind json owns are a json token already, relayed verbatim (validated: invalid json throws); a text's
    // bytes are the text they spell; any other bytes are base64.
    public void Bytes(byte[] value, global::app.type.kind.@this kind)
    {
        if (kind.Owns(this)) _writer.WriteRawValue(value);
        else if (kind.IsText) _writer.WriteStringValue(System.Text.Encoding.UTF8.GetString(value));
        else _writer.WriteBase64StringValue(value);
    }

    public void BeginArray(int count) => _writer.WriteStartArray();
    public void EndArray() => _writer.WriteEndArray();

    public void BeginObject() => _writer.WriteStartObject();
    public void Name(string name) => _writer.WritePropertyName(name);
    public void EndObject() => _writer.WriteEndObject();

    /// <summary>
    /// Writes a C# value: null, a primitive (string, int, long, double, float, bool,
    /// DateTime, decimal), byte[], <see cref="app.data.@this"/> as its record, or an
    /// <see cref="System.Collections.IEnumerable"/> of these. A plang value writes itself.
    /// </summary>
    public void Value(object? normalized)
    {
        switch (normalized)
        {
            case null: Null(); return;
            case bool b: Bool(b); return;
            case int i: Int(i); return;
            case long l: Long(l); return;
            case float f: Float(f); return;
            case double d: Double(d); return;
            case decimal dec: Decimal(dec); return;
            case string s: String(s); return;
            case System.DateTime dt: DateTime(dt); return;
            case System.DateTimeOffset dto: DateTimeOffset(dto); return;
            case System.TimeSpan ts: TimeSpan(ts); return;
            case System.Guid g: Guid(g); return;
            case System.Enum e: Enum(e); return;
            case byte[] bytes: Bytes(bytes, _binary); return;
            // A plang value is never handed here, nor a collection: each writes itself through its own Output.
            default:
                // Reaching here means a caller handed the writer a C# value whose
                // runtime type isn't in the tree contract. Falling back to STJ would
                // reflect every public property and bypass [Out]/[Sensitive]/[Masked]
                // discipline — the wire could leak fields the filter excludes.
                // Fail closed instead.
                throw new app.data.NormalizeException(
                    $"json.Writer received a value of type {normalized.GetType().FullName} that isn't part of the tree contract; a value of this type has to write itself.",
                    "NormalizeUnexpectedLeafType");
        }
    }
}
