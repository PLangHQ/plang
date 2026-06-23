using System.Globalization;
using System.IO;
using System.Text;

namespace app.channel.serializer.text;

/// <summary>
/// Plain-text <see cref="global::app.channel.serializer.IWriter"/> — PURE text, no json.
/// It owns the destination <see cref="Stream"/> and writes each leaf straight through as
/// it arrives (streaming, no intermediate buffer/copy). A leaf renders bare —
/// <c>hello</c>, <c>42</c>, <c>true</c>. A container has no plain-text form and never
/// reaches this writer: its text rendering is a per-format override
/// (<c>type/&lt;x&gt;/serializer/text.cs</c>) dispatched before <see cref="BeginObject"/>
/// could fire, so the structural tokens throw if ever reached.
/// <para><see cref="EmitsSchema"/> is false — the text channel carries no Data envelope.</para>
/// </summary>
public sealed class Writer : global::app.channel.serializer.IWriter
{
    private readonly Stream _stream;
    private readonly Encoding _encoding;

    public Writer(Stream stream, Encoding encoding)
    {
        _stream = stream;
        _encoding = encoding;
    }

    public string Format => "text";

    private void Write(string text) => _stream.Write(_encoding.GetBytes(text));

    public void Null() { }
    public void Bool(bool value) => Write(value ? "true" : "false");
    public void Int(int value) => Write(value.ToString(CultureInfo.InvariantCulture));
    public void Long(long value) => Write(value.ToString(CultureInfo.InvariantCulture));
    public void Float(float value) => Write(value.ToString(CultureInfo.InvariantCulture));
    public void Double(double value) => Write(value.ToString(CultureInfo.InvariantCulture));
    public void Decimal(decimal value) => Write(value.ToString(CultureInfo.InvariantCulture));
    public void String(string value) => Write(value);
    public void DateTime(System.DateTime value) => Write(value.ToString("o", CultureInfo.InvariantCulture));
    public void DateTimeOffset(System.DateTimeOffset value) => Write(value.ToString("o", CultureInfo.InvariantCulture));
    public void TimeSpan(System.TimeSpan value) => Write(value.ToString("c"));
    public void Guid(System.Guid value) => Write(value.ToString());
    public void Enum(System.Enum value) => Write(value.ToString());
    public void Bytes(byte[] value) => Write(System.Convert.ToBase64String(value));

    // A container has no plain-text form — it renders via its serializer/text.cs override
    // (json string), written through String above. Structural tokens never reach here.
    public void BeginArray(int count) => throw Structural();
    public void EndArray() => throw Structural();
    public void BeginObject() => throw Structural();
    public void Name(string name) => throw Structural();
    public void EndObject() => throw Structural();
    public void BeginRecord(global::app.data.@this record) => throw Structural();
    public void EndRecord() => throw Structural();
    public void Value(object? normalized) => throw Structural();

    private static System.NotSupportedException Structural() => new(
        "text writer is pure text — a container renders via its serializer/text.cs override, "
        + "not structural tokens.");
}
