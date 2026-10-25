using System.Text.Json;
using app.type.format;

namespace app.type.item.kind.jsonl;

/// <summary>
/// The JSON Lines writer: each record a json value, compact on its own line, ended by LF. A value written into it is
/// one record by default; a list's elements are its records (<c>item.Write(jsonl.Writer)</c>). A record writes itself
/// through the json writer, so every token below goes into the record in progress.
/// </summary>
public sealed class Writer : IWriter
{
    private static readonly byte[] End = [(byte)'\n'];

    // every character as itself: a line is no page
    private static readonly JsonWriterOptions Compact = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly System.IO.Stream _stream;
    private readonly System.IO.MemoryStream _record = new();   // the record in progress, before it is a line
    private readonly Utf8JsonWriter _utf8;
    private readonly Utf8JsonWriter _line;
    private readonly global::app.type.item.kind.json.Writer _json;

    /// <summary>A JSON Lines writer onto <paramref name="stream"/>, for <paramref name="view"/>, asked by
    /// <paramref name="context"/>.</summary>
    public Writer(System.IO.Stream stream, global::app.View view, global::app.actor.context.@this context)
    {
        _stream = stream;
        _utf8 = new Utf8JsonWriter(_record, Compact);
        _line = new Utf8JsonWriter(stream, Compact);
        _json = new global::app.type.item.kind.json.Writer(_utf8, view, context);
        Kind = context.App.type.list["item"].kind["jsonl"]!;
    }

    public global::app.type.kind.@this Kind { get; }
    public global::app.View View => _json.View;
    public global::app.actor.context.@this Context => _json.Context;

    /// <summary>A value written as JSON Lines: its records, as it says (a list its elements, any other value itself).</summary>
    public System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value) => value.Write(this);

    /// <summary><paramref name="value"/> as one record: its json, then the line's end.</summary>
    internal async System.Threading.Tasks.ValueTask Record(global::app.type.item.@this value)
    {
        await value.Output(_json);
        await Ended();
    }

    /// <summary>A Data as one record (a list's element, whole): its json, then the line's end.</summary>
    internal async System.Threading.Tasks.ValueTask Record(global::app.data.@this element)
    {
        await element.Output(_json);
        await Ended();
    }

    // The record written out as one line, then its end. A compact json writer writes no line break itself (a string
    // escapes its own), so one in the record came from pasted json content: the record is then laid again on one line.
    private async System.Threading.Tasks.ValueTask Ended()
    {
        await _utf8.FlushAsync(Context.CancellationToken);
        var record = _record.GetBuffer().AsMemory(0, (int)_record.Length);
        if (record.Span.IndexOfAny((byte)'\n', (byte)'\r') < 0)
            await _stream.WriteAsync(record, Context.CancellationToken);
        else
        {
            using var document = JsonDocument.Parse(record);
            document.RootElement.WriteTo(_line);
            await _line.FlushAsync(Context.CancellationToken);
            _line.Reset();
        }
        _record.SetLength(0);
        _utf8.Reset();
        await _stream.WriteAsync(End, Context.CancellationToken);
    }

    // A line is a whole record: a token written beside one (a Debug view's "written" around a template) would split a
    // record across lines and leave the rest unsent, so each refuses — the encode answers it as JsonSerializeError.
    private const string Whole = "JSON Lines writes whole records, never a bare token";

    public void Null() => throw new System.InvalidOperationException(Whole);
    public void Bool(bool value) => throw new System.InvalidOperationException(Whole);
    public void Int(int value) => throw new System.InvalidOperationException(Whole);
    public void Long(long value) => throw new System.InvalidOperationException(Whole);
    public void Float(float value) => throw new System.InvalidOperationException(Whole);
    public void Double(double value) => throw new System.InvalidOperationException(Whole);
    public void String(string value) => throw new System.InvalidOperationException(Whole);
    public void DateTime(System.DateTime value) => throw new System.InvalidOperationException(Whole);
    public void DateTimeOffset(System.DateTimeOffset value) => throw new System.InvalidOperationException(Whole);
    public void TimeSpan(System.TimeSpan value) => throw new System.InvalidOperationException(Whole);
    public void Guid(System.Guid value) => throw new System.InvalidOperationException(Whole);
    public void Enum(System.Enum value) => throw new System.InvalidOperationException(Whole);
    public void Decimal(decimal value) => throw new System.InvalidOperationException(Whole);
    public void Bytes(byte[] value, global::app.type.kind.@this kind) => throw new System.InvalidOperationException(Whole);
    public void BeginArray(int count) => throw new System.InvalidOperationException(Whole);
    public void EndArray() => throw new System.InvalidOperationException(Whole);
    public void BeginObject() => throw new System.InvalidOperationException(Whole);
    public void Name(string name) => throw new System.InvalidOperationException(Whole);
    public void EndObject() => throw new System.InvalidOperationException(Whole);
    public void Value(object? normalized) => throw new System.InvalidOperationException(Whole);
}
