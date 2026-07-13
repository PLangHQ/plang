using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace app.channel.serializer.text;

/// <summary>
/// Plain-text <see cref="global::app.channel.serializer.IWriter"/>. A TOP-LEVEL scalar renders
/// BARE — <c>hello</c>, <c>42</c>, <c>true</c> — the text channel's whole point (no quotes, no
/// envelope). Structural content has no bare-text form, so the writer renders it as JSON:
/// <see cref="BeginObject"/>/<see cref="BeginArray"/> follow the json shape (<c>{"name":…}</c>),
/// delegated to a <see cref="Utf8JsonWriter"/> over the SAME stream. The WRITER owns that — there
/// is no per-type <c>serializer/text.cs</c> override and no shape branch at the selector: a leaf
/// writes itself bare, a container writes itself as json, both through this one writer.
/// <para><see cref="IWriter.EmitsSchema"/> stays false — the text channel carries no Data envelope.</para>
/// </summary>
public sealed class Writer : global::app.channel.serializer.IWriter
{
    private readonly Stream _stream;
    private readonly Encoding _encoding;
    private global::app.channel.serializer.json.Writer? _json;   // started lazily when structure opens
    private Utf8JsonWriter? _utf8;
    private int _depth;                                          // open object/array nesting

    public Writer(Stream stream, Encoding encoding)
    {
        _stream = stream;
        _encoding = encoding;
    }

    public string Format => "text";

    private void Bare(string text) => _stream.Write(_encoding.GetBytes(text));

    // Structural content becomes json — the writer knows how, no type switch. Started lazily so a
    // pure scalar never allocates a json writer; shared stream is safe because a single value is
    // EITHER a bare top-level scalar OR structural json, never interleaved.
    private global::app.channel.serializer.json.Writer Structural()
        => _json ??= new global::app.channel.serializer.json.Writer(
               _utf8 = new Utf8JsonWriter(_stream), emitsSchema: false);

    // Scalars: bare at the top, json (quoted / comma-joined) when nested inside an open structure.
    public void Null() { if (_depth > 0) Structural().Null(); }
    public void Bool(bool value) { if (_depth > 0) Structural().Bool(value); else Bare(value ? "true" : "false"); }
    public void Int(int value) { if (_depth > 0) Structural().Int(value); else Bare(value.ToString(CultureInfo.InvariantCulture)); }
    public void Long(long value) { if (_depth > 0) Structural().Long(value); else Bare(value.ToString(CultureInfo.InvariantCulture)); }
    public void Float(float value) { if (_depth > 0) Structural().Float(value); else Bare(value.ToString(CultureInfo.InvariantCulture)); }
    public void Double(double value) { if (_depth > 0) Structural().Double(value); else Bare(value.ToString(CultureInfo.InvariantCulture)); }
    public void Decimal(decimal value) { if (_depth > 0) Structural().Decimal(value); else Bare(value.ToString(CultureInfo.InvariantCulture)); }
    public void String(string value) { if (_depth > 0) Structural().String(value); else Bare(value); }
    public void Raw(string value) { if (_depth > 0) Structural().Raw(value); else Bare(value); }
    public void DateTime(System.DateTime value) { if (_depth > 0) Structural().DateTime(value); else Bare(value.ToString("o", CultureInfo.InvariantCulture)); }
    public void DateTimeOffset(System.DateTimeOffset value) { if (_depth > 0) Structural().DateTimeOffset(value); else Bare(value.ToString("o", CultureInfo.InvariantCulture)); }
    public void TimeSpan(System.TimeSpan value) { if (_depth > 0) Structural().TimeSpan(value); else Bare(value.ToString("c")); }
    public void Guid(System.Guid value) { if (_depth > 0) Structural().Guid(value); else Bare(value.ToString()); }
    public void Enum(System.Enum value) { if (_depth > 0) Structural().Enum(value); else Bare(value.ToString()); }
    public void Bytes(byte[] value) { if (_depth > 0) Structural().Bytes(value); else Bare(System.Convert.ToBase64String(value)); }

    // Structure: always json; flush the buffered json when the outermost closes.
    public void BeginArray(int count) { Structural().BeginArray(count); _depth++; }
    public void EndArray() { Structural().EndArray(); Close(); }
    public void BeginObject() { Structural().BeginObject(); _depth++; }
    public void Name(string name) => Structural().Name(name);
    public void EndObject() { Structural().EndObject(); Close(); }
    public void BeginRecord(global::app.data.@this record) { Structural().BeginRecord(record); _depth++; }
    public void EndRecord() => throw new System.InvalidOperationException(
        "text.Writer.EndRecord requires the Data record — records ride the json delegate's EndRecord(Data).");

    // A top-level scalar rides bare (its own scalar method, depth 0); a top-level container is json.
    public void Value(object? normalized)
    {
        if (_depth > 0) { Structural().Value(normalized); return; }
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
            default:                                    // container / Data / enumerable → json content
                Structural().Value(normalized);
                _utf8!.Flush();
                return;
        }
    }

    private void Close() { if (--_depth == 0) _utf8!.Flush(); }
}
