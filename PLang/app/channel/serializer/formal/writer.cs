using System.Globalization;
using System.Text;

namespace app.channel.serializer.formal;

/// <summary>
/// Formal — a program's actions written as calls, the one text form of an action:
/// <c>file.read(Path: path = "notes.txt"); variable.set(Name: variable = %content%, Value: item = %!data%)</c>.
///
/// <para>A format beside <see cref="json.Writer"/> and the text writer. Values write themselves through it
/// unchanged — a text pushes <see cref="String"/> (quoted), a number <see cref="Long"/> (bare), a list its
/// array (<c>[a, b]</c>), a dict its object (<c>{"k": v}</c>, keys quoted). The action, its property rows and
/// a list of arguments write their own shapes through the structure below, each in its formal branch
/// (<see cref="Token"/> is the <see cref="IWriter.Format"/> they branch on): a call
/// (<see cref="BeginCall"/>), a row (<see cref="Row"/>), a body (<see cref="BeginBody"/>); a modifier is
/// the next call after its action. The writer owns the layout — separators and quoting — so the text is
/// the same byte for byte whoever writes it.</para>
/// </summary>
public sealed class Writer : IWriter
{
    /// <summary>The format token owners branch on.</summary>
    public const string Token = "formal";

    public string Format => Token;

    private readonly StringBuilder _out = new();

    // What the next value sits in: its separator, and whether one was written yet.
    private enum Frame { Top, Array, Dict, Rows, Call, Actions, Body, Record }
    private readonly Stack<(Frame Kind, int Count)> _frames = new();

    public Writer() => _frames.Push((Frame.Top, 0));

    /// <summary>The formal written so far.</summary>
    public override string ToString() => _out.ToString();

    // A value starting in a sequence takes the sequence's separator first. In a dict, a row list or a
    // call the separator came with the name (Name / Row), so the value follows it directly.
    private void Element()
    {
        var (kind, count) = _frames.Pop();
        if (count > 0)
            _out.Append(kind switch { Frame.Array => ", ", Frame.Actions or Frame.Body => "; ", _ => "" });
        _frames.Push((kind, count + 1));
    }

    private void Open(Frame kind) => _frames.Push((kind, 0));

    private void Close() => _frames.Pop();

    // ---------------------------------------------------------------- leaves

    public void Null() { Element(); _out.Append("null"); }
    public void Bool(bool value) { Element(); _out.Append(value ? "true" : "false"); }
    public void Int(int value) { Element(); _out.Append(value.ToString(CultureInfo.InvariantCulture)); }
    public void Long(long value) { Element(); _out.Append(value.ToString(CultureInfo.InvariantCulture)); }
    public void Float(float value) => Double(value);

    /// <summary>A number in its shortest round-trip form, as JSON writes it: <c>0.24</c>, <c>10000</c>.</summary>
    public void Double(double value)
    {
        Element();
        _out.Append(value.ToString("R", CultureInfo.InvariantCulture));
    }

    public void Decimal(decimal value) { Element(); _out.Append(value.ToString(CultureInfo.InvariantCulture)); }
    public void String(string value) { Element(); Quote(value); }
    public void DateTime(System.DateTime value) => String(value.ToString("O", CultureInfo.InvariantCulture));
    public void DateTimeOffset(System.DateTimeOffset value) => String(value.ToString("O", CultureInfo.InvariantCulture));
    public void TimeSpan(System.TimeSpan value) => String(value.ToString("c", CultureInfo.InvariantCulture));
    public void Guid(System.Guid value) => String(value.ToString());
    public void Enum(System.Enum value) => String(value.ToString());
    public void Bytes(byte[] value) => String(System.Convert.ToBase64String(value));

    /// <summary>Verbatim — content already formal (a %variable% written bare).</summary>
    public void Raw(string value) { Element(); _out.Append(value); }

    /// <summary>A variable, bare: <c>%content%</c>.</summary>
    public void Variable(string name) => Raw($"%{name}%");

    // A text in double quotes, escaped as JSON escapes it — quote, backslash and control characters;
    // every other character as itself.
    private void Quote(string value)
    {
        _out.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': _out.Append("\\\""); break;
                case '\\': _out.Append("\\\\"); break;
                case '\n': _out.Append("\\n"); break;
                case '\r': _out.Append("\\r"); break;
                case '\t': _out.Append("\\t"); break;
                case '\b': _out.Append("\\b"); break;
                case '\f': _out.Append("\\f"); break;
                default:
                    if (c < 0x20) _out.Append("\\u").Append(((int)c).ToString("x4"));
                    else _out.Append(c);
                    break;
            }
        }
        _out.Append('"');
    }

    // ---------------------------------------------------------------- lists and dicts

    public void BeginArray(int count) { Element(); _out.Append('['); Open(Frame.Array); }
    public void EndArray() { Close(); _out.Append(']'); }

    /// <summary>A dict literal: <c>{"k": v}</c> — its keys quoted, so it never reads as argument rows.</summary>
    public void BeginObject() { Element(); _out.Append('{'); Open(Frame.Dict); }

    public void Name(string name)
    {
        if (_frames.Peek().Kind == Frame.Record) Close();   // the previous entry's value is done
        var (kind, count) = _frames.Pop();
        if (count > 0) _out.Append(", ");
        _frames.Push((kind, count + 1));
        Quote(name);
        _out.Append(": ");
        Open(Frame.Record);   // the value that follows is the entry's own: no separator of its own
    }

    public void EndObject()
    {
        // an entry's value frame closes when the next name or the dict's end comes
        if (_frames.Peek().Kind == Frame.Record) Close();
        Close();
        _out.Append('}');
    }

    // ---------------------------------------------------------------- a Data record

    /// <summary>Formal carries no Data envelope — a record is its value.</summary>
    public void BeginRecord(global::app.data.@this record) { }
    public void EndRecord() { }

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
            case decimal m: Decimal(m); return;
            case string s: String(s); return;
            case byte[] bytes: Bytes(bytes); return;
            case global::app.data.@this nested: Value(nested.Peek()); return;
            case global::app.type.item.@this v: v.Write(this); return;
            default:
                throw new global::app.data.NormalizeException(
                    $"formal.Writer received a value of type {normalized.GetType().FullName} that has no formal form.",
                    "NormalizeUnexpectedLeafType");
        }
    }

    // ---------------------------------------------------------------- the program's structure

    /// <summary>A call: <c>module.name(</c> — its rows follow, then <see cref="EndCall"/>.</summary>
    public void BeginCall(string module, string name)
    {
        Element();
        _out.Append(module).Append('.').Append(name).Append('(');
        Open(Frame.Call);
    }

    public void EndCall()
    {
        if (_frames.Peek().Kind == Frame.Record) Close();
        Close();
        _out.Append(')');
    }

    /// <summary>A row — a call's property or an argument: <c>Name: type = </c>, <c>?=</c> for a frozen
    /// default; its value follows.</summary>
    public void Row(string name, string type, bool frozen = false)
    {
        if (_frames.Peek().Kind == Frame.Record) Close();
        var (kind, count) = _frames.Pop();
        if (count > 0) _out.Append(", ");
        _frames.Push((kind, count + 1));
        _out.Append(name).Append(": ").Append(type).Append(frozen ? " ?= " : " = ");
        Open(Frame.Record);
    }

    /// <summary>Argument rows: <c>{kind: text = "x", path: item = %path%}</c>.</summary>
    public void BeginRows() { Element(); _out.Append('{'); Open(Frame.Rows); }

    public void EndRows()
    {
        if (_frames.Peek().Kind == Frame.Record) Close();
        Close();
        _out.Append('}');
    }

    /// <summary>A sequence of actions: <c>a; b</c> — a step's, or a condition's body inside
    /// <see cref="BeginBody"/>.</summary>
    public void BeginActions() { Element(); Open(Frame.Actions); }
    public void EndActions() => Close();

    /// <summary>A condition's body, inline: <c> { a; b }</c>.</summary>
    public void BeginBody() { _out.Append(" { "); Open(Frame.Body); }
    public void EndBody() { Close(); _out.Append(" }"); }
}
