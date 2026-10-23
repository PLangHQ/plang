using System.Globalization;
using System.Text;
using app.type.format;

namespace app.goal.step.action.formal;

/// <summary>
/// Formal — a program's actions written as calls, the one text form of an action:
/// <c>file.read(Path: path = "notes.txt"); variable.set(Name: variable = %content%, Value: item = %!data%)</c>.
///
/// <para>A format beside <see cref="global::app.type.item.kind.json.Writer"/> and the text writer. Values write themselves through it
/// unchanged — a text pushes <see cref="String"/> (quoted), a number <see cref="Long"/> (bare), a list its
/// array (<c>[a, b]</c>), a dict its object (<c>{"k": v}</c>, keys quoted). The action, its property rows and
/// a list of arguments write their own shapes through the structure below, each in its formal branch
/// (<see cref="Token"/> is the <see cref="IWriter.Format"/> they branch on): a call
/// (<see cref="BeginCall"/>), a row (<see cref="Row"/>), a body (<see cref="BeginBody"/>); a clause
/// (<c>on.error</c>, …) is the next call after its action. The writer owns the layout — separators and quoting — so the text is
/// the same byte for byte whoever writes it.</para>
/// </summary>
public sealed class Writer : IWriter
{
    /// <summary>The format's name: list's <c>formal</c>.</summary>
    public const string Token = "formal";

    /// <summary>List's <c>formal</c> — what a list of actions is written as.</summary>
    public global::app.type.kind.@this Kind { get; }

    /// <summary>Formal is program text: what the step gave, as written — the Store view, always.</summary>
    public global::app.View View => global::app.View.Store;

    public global::app.actor.context.@this Context { get; }

    /// <summary>A value written in formal: its formal look (an action its call), else its own write.</summary>
    public System.Threading.Tasks.ValueTask Write(global::app.type.item.@this value) => value.Write(this);

    private readonly StringBuilder _out = new();

    // What the next value sits in: its separator, and whether one was written yet.
    private enum Frame { Top, Array, Dict, Rows, Call, Actions, Body, Record }
    private readonly Stack<(Frame Kind, int Count)> _frames = new();

    /// <summary>A formal writer asked by <paramref name="context"/>, its kind found among the asker's types.</summary>
    public Writer(global::app.actor.context.@this context) : this(context.App.type.list["list"].kind[Token]!, context) { }

    // a writer of the same kind as one in hand (an option's value, written apart)
    private Writer(global::app.type.kind.@this kind, global::app.actor.context.@this context)
    {
        Kind = kind;
        Context = context;
        _frames.Push((Frame.Top, 0));
    }

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

    // formal's view is Store for the record layout, never because it writes plang's store: characters nobody should
    // see stay masked
    public void Sensitive(string characters) => String("****");
    public void DateTime(System.DateTime value) => String(value.ToString("O", CultureInfo.InvariantCulture));
    public void DateTimeOffset(System.DateTimeOffset value) => String(value.ToString("O", CultureInfo.InvariantCulture));
    /// <summary>A length of time in ISO 8601 (<c>"PT5S"</c>) — the form the duration type teaches and reads, bare
    /// or quoted.</summary>
    public void TimeSpan(System.TimeSpan value) => String(System.Xml.XmlConvert.ToString(value));
    public void Guid(System.Guid value) => String(value.ToString());
    public void Enum(System.Enum value) => String(value.ToString());
    /// <summary>A text's bytes are the text they spell; any other bytes are base64.</summary>
    public void Bytes(byte[] value, global::app.type.kind.@this kind)
        => String(kind.IsText ? System.Text.Encoding.UTF8.GetString(value) : System.Convert.ToBase64String(value));

    // Verbatim — content already formal (a %variable% written bare).
    private void Raw(string value) { Element(); _out.Append(value); }

    /// <summary>A variable, bare: <c>%content%</c>.</summary>
    public void Variable(string name) => Raw($"%{name}%");

    /// <summary>A slot still to fill writes nothing: its option stands as its bare name (<see cref="Option"/>).</summary>
    public void Given() { }

    /// <summary>An option as a starting line writes it: <c>Template="plang"</c>, or its bare name — a slot still to
    /// fill — when its value writes nothing (<c>Permission</c>: the step gives it, the writer fills it).</summary>
    public async System.Threading.Tasks.ValueTask Option(string name, global::app.type.item.@this value)
    {
        var writer = new Writer(Kind, Context);
        await value.Output(writer);
        Element();
        _out.Append(name);
        if (writer._out.Length > 0) _out.Append('=').Append(writer._out);
    }

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
            case byte[] bytes: String(System.Convert.ToBase64String(bytes)); return;
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
    /// default; the type named by the word it goes by, its value following.</summary>
    public void Row(string name, global::app.type.@this type, bool frozen = false)
    {
        if (_frames.Peek().Kind == Frame.Record) Close();
        var (kind, count) = _frames.Pop();
        if (count > 0) _out.Append(", ");
        _frames.Push((kind, count + 1));
        _out.Append(name).Append(": ").Append(type.Face).Append(frozen ? " ?= " : " = ");
        Open(Frame.Record);
    }

    // whether each Data written opened its record, so its end closes what it opened
    private readonly Stack<bool> _records = new();

    /// <summary>A Data that is an element of a sequence (an argument in a list) is its record:
    /// <c>{name: type = value}</c>, its value following. A Data written after a name (a dict's entry) or with no name of
    /// its own is its value alone.</summary>
    public System.Threading.Tasks.ValueTask BeginRecord(global::app.data.@this record, bool layer)
    {
        var opens = _frames.Peek().Kind == Frame.Array && !string.IsNullOrEmpty(record.Name);
        _records.Push(opens);
        if (!opens) return default;
        Element();
        _out.Append('{');
        Open(Frame.Rows);
        Row(record.Name, record.Type);
        return default;
    }

    public System.Threading.Tasks.ValueTask EndRecord(global::app.data.@this record)
    {
        if (!_records.Pop()) return default;
        if (_frames.Peek().Kind == Frame.Record) Close();
        Close();
        _out.Append('}');
        return default;
    }

    /// <summary>A sequence of actions: <c>a; b</c> — a step's, or a condition's body inside
    /// <see cref="BeginBody"/>. Held as a row's value (on.error's Recovery) it is a list: <c>[a, b]</c>.</summary>
    public void BeginActions()
    {
        if (_frames.Peek().Kind == Frame.Record) { BeginArray(0); return; }
        Element();
        Open(Frame.Actions);
    }

    public void EndActions()
    {
        if (_frames.Peek().Kind == Frame.Array) { EndArray(); return; }
        Close();
    }

    /// <summary>A condition's body, inline: <c> { a; b }</c>.</summary>
    public void BeginBody() { _out.Append(" { "); Open(Frame.Body); }
    public void EndBody() { Close(); _out.Append(" }"); }
}
