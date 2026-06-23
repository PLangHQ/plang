using System.Text.Json;

namespace app.channel.serializer.json;

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
public sealed class Writer : IWriter
{
    private readonly Utf8JsonWriter _writer;
    private readonly JsonSerializerOptions _options;
    private readonly app.View _view;
    private readonly app.type.renderer.@this? _renderers;
    private readonly bool _emitsSchema;

    public Writer(Utf8JsonWriter writer, JsonSerializerOptions? options = null,
        app.View view = app.View.Out, app.type.renderer.@this? renderers = null,
        bool emitsSchema = false)
    {
        _writer = writer;
        _options = options ?? new JsonSerializerOptions();
        _view = view;
        _renderers = renderers;
        _emitsSchema = emitsSchema;
    }

    public string Format => _emitsSchema ? "plang" : "json";

    /// <summary>The plang wire carries the @schema/type envelope; plain json is bare.</summary>
    public bool EmitsSchema => _emitsSchema;

    public void Null() => _writer.WriteNullValue();
    public void Bool(bool value) => _writer.WriteBooleanValue(value);
    public void Int(int value) => _writer.WriteNumberValue(value);
    public void Long(long value) => _writer.WriteNumberValue(value);
    public void Float(float value) => _writer.WriteNumberValue(value);
    public void Double(double value) => _writer.WriteNumberValue(value);
    public void String(string value) => _writer.WriteStringValue(value);
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
        // @schema:data — every Data marks itself, nested ones too, so the read side
        // recognizes a Data inside a value the same way as the top-level one.
        _writer.WriteString(global::app.data.@this.WireSchema, global::app.data.@this.WireSchemaData);
        // The binding label rides only on the Store view (.pr parameters bind by
        // name); the outbound wire omits it — a server's variable name is not
        // API surface a client should couple to.
        if (_view == app.View.Store)
            _writer.WriteString("name", record.Name);

        var typeVal = record.Type?.Name;
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
                // Route through the same Value pipeline as the value slot,
                // honoring the writer's configured view. Hard-coding View.Out
                // here would silently strip [Sensitive] and [Store]-only
                // fields when an inner Data is emitted inline during a
                // Store-mode walk.
                var normalized = app.data.@this.NormalizeValue(kvp.Value, _view,
                    new HashSet<object>(System.Collections.Generic.ReferenceEqualityComparer.Instance),
                    depth: 0);
                Value(normalized);
            }
            _writer.WriteEndObject();
        }

        // Signing is no longer a Data property — a signed payload is a `signature`
        // layer wrapping the data record (emitted hoisted at the wire boundary),
        // not a `signature` field inside the envelope.
        _writer.WriteEndObject();
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
            case app.data.@this nested:
                BeginRecord(nested);
                Value(nested.Peek());
                EndRecord(nested);
                return;
            case app.type.dict.@this dict:
                // The native object shape. Normalize hands the writer a `dict`
                // for every object form — a json-object value, a raw infra dict,
                // and a reflected C# domain record all converge here. Emit as a
                // JSON object keyed by entry name, including the empty case so a
                // record-with-no-properties round-trips as `{}` not `[]`. The
                // writer disambiguates object-vs-array by value type: `dict`→`{}`,
                // any other IEnumerable→`[]`.
                _writer.WriteStartObject();
                foreach (var entry in dict.Entries)
                {
                    _writer.WritePropertyName(entry.Name);
                    Value(entry.Peek());
                }
                _writer.WriteEndObject();
                return;
            case app.type.list.@this nativeList:
                // The native list shape. On the wire each element self-describes —
                // Value(item) routes an element Data through the record arm, so a
                // signed element carries its envelope. Disambiguated by wrapper
                // type from dict (`{}`); a bare IEnumerable still falls to `[]` below.
                BeginArray(nativeList.CountRaw);
                foreach (var item in nativeList.Items) Value(item);
                EndArray();
                return;
            // A value renders itself — leaves (text/number/bool/date-family/…) and
            // structured types (path/file/url/code/directory/image) own their wire
            // form via Write (OBP Rule 9: the value owns the mapping, the writer
            // never type-switches per type). dict/list are matched above by their
            // own cases. A type that reaches here without a Write override throws
            // loudly from the base — the build gate forbids that for value types.
            case app.type.item.@this v: v.Write(this); return;
            case System.Collections.IEnumerable list:
                BeginArray(-1);
                foreach (var item in list) Value(item);
                EndArray();
                return;
            default:
                // Reaching here means Normalize handed off a value whose runtime
                // type isn't in the tree contract. Falling back to STJ would
                // reflect every public property and bypass [Out]/[Sensitive]/[Masked]
                // discipline — the wire could leak fields the filter excludes.
                // Fail closed instead.
                throw new app.data.NormalizeException(
                    $"json.Writer received a value of type {normalized.GetType().FullName} that isn't part of the tree contract. Normalize is missing a case for this type.",
                    "NormalizeUnexpectedLeafType");
        }
    }
}
