using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;

namespace app.type.item.text;

/// <summary>
/// Plain-text <see cref="global::app.type.format.IWriter"/>. A TOP-LEVEL scalar renders
/// BARE — <c>hello</c>, <c>42</c>, <c>true</c> — the text channel's whole point (no quotes, no
/// envelope). Structural content has no bare-text form, so the writer renders it as JSON:
/// <see cref="BeginObject"/>/<see cref="BeginArray"/> follow the json shape (<c>{"name":…}</c>),
/// delegated to a <see cref="Utf8JsonWriter"/> over the SAME stream. The WRITER owns that — there
/// is no per-type <c>serializer/text.cs</c> override and no shape branch at the selector: a leaf
/// writes itself bare, a container writes itself as json, both through this one writer.
/// <para>A Data shows only its value here: the record door writes nothing.</para>
/// </summary>
public sealed class Writer : global::app.type.format.IWriter
{
    private readonly Stream _stream;
    private readonly Encoding _encoding;
    private global::app.type.item.kind.json.Writer? _json;   // started lazily when structure opens
    private Utf8JsonWriter? _utf8;
    private int _depth;                                          // open object/array nesting
    private readonly global::app.type.item.culture.@this _culture;
    private readonly global::app.type.list.@this _types;

    /// <summary>A writer of text into <paramref name="stream"/>, its numbers read as <paramref name="culture"/>
    /// reads them (the asker's <c>%!app.setting.culture%</c>), for <paramref name="view"/>, asked by
    /// <paramref name="context"/>; its kind is text's own format.</summary>
    public Writer(Stream stream, Encoding encoding, global::app.type.item.culture.@this culture, global::app.View view,
        global::app.actor.context.@this context)
    {
        _stream = stream;
        _encoding = encoding;
        _culture = culture;
        View = view;
        Context = context;
        _types = context.App.type.list;
        Kind = _types["text"].kind;
    }

    public global::app.type.kind.@this Kind { get; }
    public global::app.View View { get; }
    public global::app.actor.context.@this Context { get; }

    /// <summary>A value written as text: its text look (a type its word), else its own write.</summary>
    public System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value) => value.Write(this);

    private void Bare(string text) => _stream.Write(_encoding.GetBytes(text));

    // Structural content becomes json — the writer knows how, no type switch. Started lazily so a
    // pure scalar never allocates a json writer; shared stream is safe because a single value is
    // EITHER a bare top-level scalar OR structural json, never interleaved.
    private global::app.type.item.kind.json.Writer Structural()
        => _json ??= new global::app.type.item.kind.json.Writer(
               _utf8 = new Utf8JsonWriter(_stream), View, Context);

    // Scalars: bare at the top, json (quoted / comma-joined) when nested inside an open structure.
    public void Null() { if (_depth > 0) Structural().Null(); }
    public void Bool(bool value) { if (_depth > 0) Structural().Bool(value); else Bare(value ? "true" : "false"); }
    public void Int(int value) { if (_depth > 0) Structural().Int(value); else Bare(value.ToString(CultureInfo.InvariantCulture)); }
    public void Long(long value) { if (_depth > 0) Structural().Long(value); else Bare(value.ToString(CultureInfo.InvariantCulture)); }
    public void Float(float value) { if (_depth > 0) Structural().Float(value); else Bare(Written(value)); }
    public void Double(double value) { if (_depth > 0) Structural().Double(value); else Bare(Written(value)); }
    public void Decimal(decimal value) { if (_depth > 0) Structural().Decimal(value); else Bare(Written((double)value, value)); }

    // A number as text, as the culture reads it: at most its decimals (2 for most), trailing zeros dropped, its
    // separator (7,47 in is-IS), no grouping; a non-zero value keeps its first significant digit (0.001, never 0).
    // Json and the wire write the number whole, invariant (the structural writer above).
    private string Written(double value, System.IFormattable? exact = null)
    {
        if (!double.IsFinite(value)) return value.ToString(CultureInfo.InvariantCulture);
        var places = _culture.Number.NumberDecimalDigits;
        if (value != 0 && -(int)System.Math.Floor(System.Math.Log10(System.Math.Abs(value))) is var first && first > places)
            places = first;
        var pattern = places == 0 ? "0" : "0." + new string('#', places);
        return (exact ?? value).ToString(pattern, _culture.Number);
    }
    public void String(string value) { if (_depth > 0) Structural().String(value); else Bare(value); }
    public void DateTime(System.DateTime value) { if (_depth > 0) Structural().DateTime(value); else Bare(value.ToString("o", CultureInfo.InvariantCulture)); }
    public void DateTimeOffset(System.DateTimeOffset value) { if (_depth > 0) Structural().DateTimeOffset(value); else Bare(value.ToString("o", CultureInfo.InvariantCulture)); }
    public void TimeSpan(System.TimeSpan value) { if (_depth > 0) Structural().TimeSpan(value); else Bare(value.ToString("c")); }
    public void Guid(System.Guid value) { if (_depth > 0) Structural().Guid(value); else Bare(value.ToString()); }
    public void Enum(System.Enum value) { if (_depth > 0) Structural().Enum(value); else Bare(value.ToString()); }
    // A text's bytes are the text they spell; any other bytes read as text are noise: their text form is their size.
    public void Bytes(byte[] value, global::app.type.kind.@this kind)
    {
        var text = kind.IsText ? Encoding.UTF8.GetString(value) : $"[{value.Length} bytes]";
        if (_depth > 0) Structural().String(text); else Bare(text);
    }

    // Structure: always json; flush the buffered json when the outermost closes.
    public void BeginArray(int count) { Structural().BeginArray(count); _depth++; }
    public void EndArray() { Structural().EndArray(); Close(); }
    public void BeginObject() { Structural().BeginObject(); _depth++; }
    public void Name(string name) => Structural().Name(name);
    public void EndObject() { Structural().EndObject(); Close(); }

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
            case byte[] bytes: Bytes(bytes, _types[typeof(global::app.type.item.binary.@this)].kind); return;
            // a plang value is never handed here, nor a collection: each writes itself through its own Output
            default:
                Structural().Value(normalized);
                return;
        }
    }

    private void Close() { if (--_depth == 0) _utf8!.Flush(); }
}
