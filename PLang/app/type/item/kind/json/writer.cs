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
/// the tree contract (primitives / byte[] / C# collections).</para>
/// </summary>
public sealed class Writer : IWriter
{
    private readonly Utf8JsonWriter _writer;
    private readonly app.View _view;
    private readonly bool _emitsSchema;

    /// <summary>A json writer over <paramref name="writer"/>, its kind found among <paramref name="types"/>: plang's
    /// own (wire's <c>plang</c>) while it carries the schema layer, else item's <c>json</c> — that choice goes when
    /// plang has its own writer.</summary>
    public Writer(Utf8JsonWriter writer, global::app.type.list.@this types, app.View view = app.View.Out, bool emitsSchema = false)
    {
        _writer = writer;
        _view = view;
        _emitsSchema = emitsSchema;
        Kind = emitsSchema ? types["wire"].kind["plang"]! : types["item"].kind["json"]!;
    }

    public global::app.type.kind.@this Kind { get; }

    /// <summary>The plang wire carries the @schema/type envelope; plain json is bare.</summary>
    public bool EmitsSchema => _emitsSchema;

    public void Null() => _writer.WriteNullValue();
    public void Bool(bool value) => _writer.WriteBooleanValue(value);
    public void Int(int value) => _writer.WriteNumberValue(value);
    public void Long(long value) => _writer.WriteNumberValue(value);
    public void Float(float value) => _writer.WriteNumberValue(value);
    public void Double(double value) => _writer.WriteNumberValue(value);
    public void String(string value) => _writer.WriteStringValue(value);
    // a sensitive value is kept whole in plang's own store, and shown masked in every other view
    public void Sensitive(string characters) => String(_view == app.View.Store ? characters : "****");
    // Verbatim — raw json (object/number) rides inline, unquoted.
    public void Raw(string value) => _writer.WriteRawValue(value);
    // Verbatim from UTF-8 bytes — validated as json the same way (invalid bytes or json throw).
    public void Raw(byte[] value) => _writer.WriteRawValue(value);
    public void DateTime(System.DateTime value) => _writer.WriteStringValue(value);
    public void DateTimeOffset(System.DateTimeOffset value) => _writer.WriteStringValue(value);
    public void TimeSpan(System.TimeSpan value) => _writer.WriteStringValue(value.ToString("c"));
    public void Guid(System.Guid value) => _writer.WriteStringValue(value);
    public void Enum(System.Enum value) => _writer.WriteStringValue(value.ToString());
    public void Decimal(decimal value) => _writer.WriteNumberValue(value);
    public void Bytes(byte[] value) => _writer.WriteBase64StringValue(value);

    public void BeginArray(int count) => _writer.WriteStartArray();
    public void EndArray() => _writer.WriteEndArray();

    public void BeginObject() => _writer.WriteStartObject();
    public void Name(string name) => _writer.WritePropertyName(name);
    public void EndObject() => _writer.WriteEndObject();

    /// <summary>A Data's record, in bare json: nothing of its own — bare json shows only the value.</summary>
    public void BeginRecord(app.data.@this record) { }

    /// <summary>A Data's record closes: nothing of its own in bare json.</summary>
    public void EndRecord(app.data.@this record) { }

    void IWriter.EndRecord() => throw new System.InvalidOperationException(
        "JsonWriter.EndRecord requires the Data record reference — call EndRecord(Data) instead.");

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
            case byte[] bytes: Bytes(bytes); return;
            // A plang value is never handed here: it writes itself through its own Output.
            case System.Collections.IEnumerable list:
                BeginArray(-1);
                foreach (var item in list) Value(item);
                EndArray();
                return;
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
