using System.IO;
using System.Text;

namespace app.module.llm;

/// <summary>
/// The llm module's own <see cref="global::app.type.format.IWriter"/> — how a value looks when it is
/// written into an LLM message. A value writes what it is through the writer's primitives, as always;
/// this writer decides how each looks for a model that pays per character and has a context it can blow.
///
/// <para>Stage 1: a text over <c>%!llm.setting.length%</c> characters is cut, and a marker tells the
/// model how much more there was — <c>--- N more characters ---</c>, N the characters dropped. Short
/// text rides whole. Everything else is formatted exactly as the text writer does it (a leaf bare, a
/// container as its json), so a value the writer does not yet cut is unchanged. The list/dict column
/// header and row cap (stage 2) and the binary/image label (stage 3) grow here, on this writer, not on
/// any value.</para>
///
/// <para>It is a thin decorator over <see cref="global::app.type.item.text.Writer"/>: the inner writer
/// owns the formatting (bare scalars, structure as json, the same stream), and this one caps the
/// character content handed to <see cref="String"/> / <see cref="Content"/> / <see cref="Raw(string)"/>.
/// For a full-match text message content — one top-level string — that single call is the whole value,
/// so the cap is the value's cut.</para>
/// </summary>
public sealed class Writer : global::app.type.format.IWriter
{
    // the base text writer over the stream (restore target); _inner is normally it, but becomes a Table
    // while a top-level list/dict is being gathered. Typed as the interface so Content / Raw(byte[]) reach
    // IWriter's default members, which route through the inner writer's own String / Raw.
    private readonly global::app.type.format.IWriter _base;
    private global::app.type.format.IWriter _inner;
    private readonly int _length;
    private readonly int _rows;
    private readonly global::app.type.item.culture.@this _culture;

    // while a top-level list/dict is gathered into a Table: _structDepth counts the nesting so only the
    // OUTERMOST open swaps to a Table and its matching close flushes the markdown.
    private int _structDepth;
    private Table? _table;

    public Writer(Stream stream, Encoding encoding, global::app.type.item.culture.@this culture, int length, int rows)
    {
        _base = new global::app.type.item.text.Writer(stream, encoding, culture);
        _inner = _base;
        _length = length;
        _rows = rows;
        _culture = culture;
    }

    public string Format => "llm";

    // The cut: a text within the limit rides whole; one over it keeps its first _length characters and
    // then says how much more there was, so the model reads a true, bounded view of the value.
    private string Cut(string value)
        => value.Length <= _length
            ? value
            : value[.._length] + $"\n--- {value.Length - _length} more characters ---";

    // Character content — capped, then written as the inner writer would (it decides bare vs quoted,
    // and, for Content, text-of-a-kind vs plain).
    public void String(string value) => _inner.String(Cut(value));
    public void Content(string value, global::app.type.kind.@this kind) => _inner.Content(Cut(value), kind);
    public void Raw(string value) => _inner.Raw(Cut(value));

    // Everything else — unchanged, through the inner text writer (same stream).
    public void Null() => _inner.Null();
    public void Bool(bool value) => _inner.Bool(value);
    public void Int(int value) => _inner.Int(value);
    public void Long(long value) => _inner.Long(value);
    public void Float(float value) => _inner.Float(value);
    public void Double(double value) => _inner.Double(value);
    public void Decimal(decimal value) => _inner.Decimal(value);
    public void DateTime(System.DateTime value) => _inner.DateTime(value);
    public void DateTimeOffset(System.DateTimeOffset value) => _inner.DateTimeOffset(value);
    public void TimeSpan(System.TimeSpan value) => _inner.TimeSpan(value);
    public void Guid(System.Guid value) => _inner.Guid(value);
    public void Enum(System.Enum value) => _inner.Enum(value);
    public void Bytes(byte[] value) => _inner.Bytes(value);
    public void Raw(byte[] value) => _inner.Raw(value);

    // A top-level list or dict is gathered into a Table (built from this same primitive stream) and
    // emitted as a readable markdown view at its matching close. Only the OUTERMOST structural open
    // swaps the sink; nested opens flow into the Table, which summarizes them.
    public void BeginArray(int count) { Open(isList: true); _inner.BeginArray(count); }
    public void BeginObject() { Open(isList: false); _inner.BeginObject(); }
    public void EndArray() { _inner.EndArray(); Close(); }
    public void EndObject() { _inner.EndObject(); Close(); }
    public void Name(string name) => _inner.Name(name);

    private void Open(bool isList)
    {
        if (_structDepth++ == 0) { _table = new Table(_rows, _culture, isList); _inner = _table; }
    }

    private void Close()
    {
        if (--_structDepth == 0 && _table is { } table)
        {
            _inner = _base;
            _base.Raw(table.Markdown());
            _table = null;
        }
    }
    public void BeginRecord(global::app.data.@this record) => _inner.BeginRecord(record);
    public void EndRecord() => _inner.EndRecord();
    public void Value(object? normalized) => _inner.Value(normalized);

    // A text with values inside it ("rank these %orders%"): the sink is THIS writer — the parts write
    // straight through it, so each embedded value is cut as it passes String/Content (and, at stage 2, its
    // own structural cuts) and the literals pass whole. No buffering: the cut lives in the per-value
    // primitives, which is the whole point of the door for the llm writer.
    public global::app.type.format.IWriter BeginText() => this;
    public void EndText() { }
}
