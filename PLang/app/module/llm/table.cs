using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace app.module.llm;

/// <summary>
/// Builds a readable markdown view of a list or a dict written into an LLM message, from the primitive
/// stream the value itself emits (BeginArray / BeginObject / Name / the scalar primitives) — it never
/// inspects the value's class. The llm <see cref="Writer"/> swaps itself for a Table on the outermost
/// structural open and emits <see cref="Markdown"/> at the matching close.
///
/// <para>A list of records → a table: its columns with their types, then up to <c>rows</c> rows, then
/// <c>--- N more rows ---</c>. A list of scalars → a one-column table. A dict → its keys with their
/// types and values, cut with <c>--- N more keys ---</c>. A cell that is itself a list or a dict → a
/// bounded summary (<c>[list of 3]</c>, <c>{dict of 4 keys}</c>), never inlined recursively.</para>
///
/// <para><b>Column type is a guess</b> read from the scalar primitive a cell calls: Int/Long/Double/
/// Decimal/Float → number, DateTime → datetime, Bool → bool, Guid → guid, Bytes → binary, String → text.
/// A value that writes itself as a String — the path family (path/url/file/directory), choice, duration,
/// size — is therefore labelled <c>(text)</c>. Backlog (branch A): a writer learning a value's plang type
/// without the wire envelope.</para>
/// </summary>
internal sealed class Table : global::app.type.format.IWriter
{
    private readonly int _rows;
    private readonly global::app.type.item.culture.@this _culture;
    private readonly bool _isList;   // opened by BeginArray (a list) vs BeginObject (a dict)

    private int _depth;              // structural nesting inside the Table's own subtree

    // list mode: rows of ordered cells; dict mode: entries. One cell/entry = (name, text, type).
    private readonly List<List<(string Name, string Text, string Type)>> _rows_ = new();
    private readonly List<(string Name, string Text, string Type)> _entries = new();   // dict mode
    private int _count;              // total rows (list) / keys (dict), even past the cap

    private string? _name;           // the column/key a value is about to land under
    private List<(string Name, string Text, string Type)>? _row;   // the row being built (list mode)

    // a nested cell (list/dict inside a cell): capture its summary instead of inlining
    private int _nestDepth;          // >0 while inside a cell's own list/dict
    private bool _nestIsList;
    private int _nestCount;

    public Table(int rows, global::app.type.item.culture.@this culture, bool isList)
    {
        _rows = rows;
        _culture = culture;
        _isList = isList;
    }

    public string Format => "llm";

    // --- the stream -------------------------------------------------------------------------------

    public void BeginArray(int count)
    {
        _depth++;
        if (_depth == 1) return;                 // the outer list itself
        if (_nestDepth == 0 && _depth >= 2) { _nestDepth = 1; _nestIsList = true; _nestCount = 0; return; }
        _nestDepth++;
    }

    public void EndArray()
    {
        if (_nestDepth > 0) { if (--_nestDepth == 0) Land($"[list of {_nestCount}]", "list"); }
        _depth--;
    }

    public void BeginObject()
    {
        _depth++;
        if (_nestDepth > 0) { _nestDepth++; return; }
        if (_isList && _depth == 2) { _row = new(); return; }   // a row of a record list
        if (!_isList && _depth == 1) return;                    // the dict itself
        // a nested object inside a cell → summary
        _nestDepth = 1; _nestIsList = false; _nestCount = 0;
    }

    public void EndObject()
    {
        if (_nestDepth > 0) { if (--_nestDepth == 0) Land($"{{dict of {_nestCount} keys}}", "dict"); _depth--; return; }
        if (_isList && _depth == 2 && _row != null) { _rows_.Add(_row); _row = null; _count++; }
        _depth--;
    }

    public void Name(string name)
    {
        if (_nestDepth > 0) { if (_nestDepth == 1) _nestCount++; return; }
        _name = name;
    }

    // a scalar value lands in the current cell/entry; in a nested cell it only grows the summary count
    private void Scalar(string text, string type)
    {
        if (_nestDepth > 0) { if (_nestDepth == 1 && _nestIsList) _nestCount++; return; }
        Land(text, type);
    }

    private void Land(string text, string type)
    {
        if (_isList)
        {
            if (_row != null) _row.Add((_name ?? "", text, type));            // a record field
            else { _rows_.Add(new() { ("", text, type) }); _count++; }         // a scalar list element
        }
        else { _entries.Add((_name ?? "", text, type)); _count++; }            // a dict entry
        _name = null;
    }

    private string Num(string s) => s;
    public void Null() => Scalar("", "");
    public void Bool(bool value) => Scalar(value ? "true" : "false", "bool");
    public void Int(int value) => Scalar(value.ToString(CultureInfo.InvariantCulture), "number");
    public void Long(long value) => Scalar(value.ToString(CultureInfo.InvariantCulture), "number");
    public void Float(float value) => Scalar(value.ToString("R", CultureInfo.InvariantCulture), "number");
    public void Double(double value) => Scalar(value.ToString("R", CultureInfo.InvariantCulture), "number");
    public void Decimal(decimal value) => Scalar(value.ToString(CultureInfo.InvariantCulture), "number");
    public void String(string value) => Scalar(value, "text");
    public void Raw(string value) => Scalar(value, "text");
    public void DateTime(System.DateTime value) => Scalar(value.ToString("o", CultureInfo.InvariantCulture), "datetime");
    public void DateTimeOffset(System.DateTimeOffset value) => Scalar(value.ToString("o", CultureInfo.InvariantCulture), "datetime");
    public void TimeSpan(System.TimeSpan value) => Scalar(value.ToString("c"), "duration");
    public void Guid(System.Guid value) => Scalar(value.ToString(), "guid");
    public void Enum(System.Enum value) => Scalar(value.ToString(), "choice");
    public void Bytes(byte[] value) => Scalar($"[{value.Length} bytes]", "binary");

    public void BeginRecord(global::app.data.@this record) => BeginObject();
    public void EndRecord() => EndObject();
    public void Value(object? normalized) { if (normalized != null) Scalar(normalized.ToString() ?? "", "text"); else Null(); }

    // --- render -----------------------------------------------------------------------------------

    // markdown-cell-safe: no newlines or pipes break the table
    private static string Cell(string s) => s.Replace("\n", " ").Replace("\r", " ").Replace("|", "\\|").Trim();

    public string Markdown()
        => _isList ? List() : Dict();

    private string Dict()
    {
        var sb = new StringBuilder();
        foreach (var (name, text, type) in _entries.Take(_rows))
            sb.Append("- ").Append(name).Append(" (").Append(type == "" ? "null" : type).Append("): ")
              .Append(Cell(text)).Append('\n');
        if (_count > _rows) sb.Append("--- ").Append(_count - _rows).Append(" more keys ---\n");
        return sb.ToString().TrimEnd('\n');
    }

    private string List()
    {
        if (_rows_.Count == 0) return "(no rows)";
        var columns = _rows_[0].Select(c => c.Name).ToList();
        var types = _rows_[0].Select(c => c.Type).ToList();
        var sb = new StringBuilder();

        bool scalarList = columns.Count == 1 && columns[0] == "";
        if (!scalarList)
        {
            sb.Append("| ").Append(string.Join(" | ",
                columns.Select((c, i) => $"{c} ({(types[i] == "" ? "null" : types[i])})"))).Append(" |\n");
            sb.Append("| ").Append(string.Join(" | ", columns.Select(_ => "---"))).Append(" |\n");
        }
        foreach (var row in _rows_.Take(_rows))
        {
            if (scalarList) sb.Append("- ").Append(Cell(row[0].Text)).Append('\n');
            else sb.Append("| ").Append(string.Join(" | ", row.Select(c => Cell(c.Text)))).Append(" |\n");
        }
        if (_count > _rows) sb.Append("--- ").Append(_count - _rows).Append(" more rows ---\n");
        return sb.ToString().TrimEnd('\n');
    }
}
