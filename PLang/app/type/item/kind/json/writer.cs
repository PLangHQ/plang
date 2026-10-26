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

    /// <summary>A json writer over <paramref name="writer"/>, for <paramref name="view"/>, asked by
    /// <paramref name="context"/>; its kind item's <c>json</c> among the asker's types. Bare json shows a Data's value
    /// only (the record door writes nothing).</summary>
    public Writer(Utf8JsonWriter writer, global::app.View view, global::app.actor.context.@this context)
    {
        _writer = writer;
        View = view;
        Context = context;
        Kind = context.App.type.list["item"].kind["json"]!;
        _binary = context.App.type.list[typeof(global::app.type.item.binary.@this)].kind;
    }

    public global::app.type.kind.@this Kind { get; }
    public global::app.View View { get; }
    public global::app.actor.context.@this Context { get; }

    /// <summary>A value written as json: its json look (characters that already are json pasted), else its own write.</summary>
    public System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value) => value.Write(this);

    public void Null() => _writer.WriteNullValue();
    public void Bool(bool value) => _writer.WriteBooleanValue(value);
    public void Int(int value) => _writer.WriteNumberValue(value);
    public void Long(long value) => _writer.WriteNumberValue(value);
    public void Float(float value) => _writer.WriteNumberValue(value);
    public void Double(double value) => _writer.WriteNumberValue(value);
    public void String(string value) => _writer.WriteStringValue(value);
    // Verbatim — raw json (object/number) rides inline, unquoted; its own nesting counts where it lands.
    public void Raw(string value)
    {
        Deeper(Nesting(value));
        _writer.WriteRawValue(value);
    }
    public void DateTime(System.DateTime value) => _writer.WriteStringValue(value);
    public void DateTimeOffset(System.DateTimeOffset value) => _writer.WriteStringValue(value);
    public void TimeSpan(System.TimeSpan value) => _writer.WriteStringValue(value.ToString("c"));
    public void Guid(System.Guid value) => _writer.WriteStringValue(value);
    public void Enum(System.Enum value) => _writer.WriteStringValue(value.ToString());
    public void Decimal(decimal value) => _writer.WriteNumberValue(value);
    // A text's bytes are the text they spell; any other bytes are base64.
    public void Bytes(byte[] value, global::app.type.kind.@this kind)
    {
        if (kind.IsText) _writer.WriteStringValue(System.Text.Encoding.UTF8.GetString(value));
        else _writer.WriteBase64StringValue(value);
    }

    public void BeginArray(int count)
    {
        Deeper();
        _writer.WriteStartArray();
    }
    public void EndArray() => _writer.WriteEndArray();

    public void BeginObject()
    {
        Deeper();
        _writer.WriteStartObject();
    }

    // As deep as plang's readers read (System.Text.Json's reader default): a reader recurses per level, so a value
    // deeper than that is refused here, where it is written, never sent for a reader to fail on.
    private const int Depth = 64;

    // So many levels further in (one, for an object or array begun here), or why not.
    private void Deeper(int levels = 1)
    {
        if (_writer.CurrentDepth + levels > Depth)
            throw new global::app.error.AppException(new global::app.error.@this(
                $"a value nested deeper than {Depth} levels can't be written: plang's readers read {Depth} levels", "ValueTooDeep", 400));
    }

    // How deep the objects and arrays in a json slice go (a string's brackets are its characters, not levels).
    private int Nesting(string json)
    {
        int depth = 0, deepest = 0;
        var quoted = false;
        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (quoted)
            {
                if (c == '\\') i++;
                else if (c == '"') quoted = false;
                continue;
            }
            switch (c)
            {
                case '"': quoted = true; break;
                case '{' or '[': deepest = System.Math.Max(deepest, ++depth); break;
                case '}' or ']': depth--; break;
            }
        }
        return deepest;
    }
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
