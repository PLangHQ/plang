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
    private readonly app.types.renderers.@this? _renderers;

    public Writer(Utf8JsonWriter writer, JsonSerializerOptions? options = null,
        app.View view = app.View.Out, app.types.renderers.@this? renderers = null)
    {
        _writer = writer;
        _options = options ?? new JsonSerializerOptions();
        _view = view;
        _renderers = renderers;
    }

    public string Format => "json";

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

        if (record.Signature != null)
        {
            _writer.WritePropertyName("signature");
            JsonSerializer.Serialize(_writer, record.Signature, _options);
        }

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
            case app.data.TypedValueNode typed:
                // Per-(type, format) renderer dispatch. The build gate (PLNG)
                // makes this lookup total for built-in [PlangType] types;
                // runtime-loaded types must register at least a Default
                // renderer or the load fails (Stage 7).
                var write = _renderers?.Of(typed.TypeName, Format);
                if (write == null)
                    throw new app.data.NormalizeException(
                        $"No renderer registered for ({typed.TypeName}, {Format}) — type was tagged but no Default.cs / per-format Write was discovered.",
                        "RendererLookupMissed");
                write(typed.Value, this);
                return;
            case app.data.@this nested:
                BeginRecord(nested);
                Value(nested.Value);
                EndRecord(nested);
                return;
            case List<app.data.@this> propertyBag:
                // NormalizeObject (a domain class → property bag) always returns
                // List<Data>. Emit as JSON object — including the empty case,
                // so a record-with-no-properties (Execute) round-trips as `{}`
                // not `[]`.
                _writer.WriteStartObject();
                foreach (var child in propertyBag)
                {
                    _writer.WritePropertyName(child.Name);
                    Value(child.Value);
                }
                _writer.WriteEndObject();
                return;
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
