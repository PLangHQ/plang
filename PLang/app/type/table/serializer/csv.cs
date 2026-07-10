namespace app.type.table.serializer;

/// <summary>
/// Reader for the <c>table</c> shape encoded as <c>csv</c>. Turns the type's own
/// raw form (a csv string) into a <see cref="app.type.table.@this"/> grid — the
/// read-side half keyed at <c>(table, csv)</c> in the reader registry, the mirror
/// of <see cref="app.type.@object.serializer.json"/> for the tree shape.
///
/// <para>The first record is the header row; each following record becomes a row
/// keyed by header. RFC-4180 quoting is honoured — a quoted field may contain
/// commas, newlines, and doubled quotes (<c>""</c> → <c>"</c>).</para>
/// </summary>
public static class csv
{
    public static object? Read(object raw, string? kind, global::app.type.reader.ReadContext ctx)
    {
        // Content off I/O rides as binary bytes; the csv is text — decode through
        // the text type (it owns bytes→string), then parse.
        if (raw is not (string or byte[])) return raw;
        string text = new global::app.type.item.text.@this(raw).ToString();
        if (string.IsNullOrEmpty(text)) return new global::app.type.table.@this(System.Array.Empty<string>(),
            System.Array.Empty<IReadOnlyDictionary<string, object?>>(), kind);

        List<List<string>> records = Parse(text);
        if (records.Count == 0) return new global::app.type.table.@this(System.Array.Empty<string>(),
            System.Array.Empty<IReadOnlyDictionary<string, object?>>(), kind);

        List<string> headers = records[0];
        var rows = new List<IReadOnlyDictionary<string, object?>>(records.Count - 1);
        for (int r = 1; r < records.Count; r++)
        {
            List<string> cells = records[r];
            var row = new Dictionary<string, object?>(headers.Count, System.StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < headers.Count; c++)
                row[headers[c]] = c < cells.Count ? cells[c] : null;
            rows.Add(row);
        }
        return new global::app.type.table.@this(headers, rows, kind);
    }

    // RFC-4180 record/field split. A field is quoted when it opens with '"';
    // inside a quoted field a doubled '"' is a literal quote and commas/newlines
    // are data. Unquoted fields end at the next comma or line break.
    private static List<List<string>> Parse(string text)
    {
        var records = new List<List<string>>();
        var field = new System.Text.StringBuilder();
        var record = new List<string>();
        bool inQuotes = false;
        bool fieldStarted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
                continue;
            }

            switch (ch)
            {
                case '"' when !fieldStarted || field.Length == 0:
                    inQuotes = true;
                    fieldStarted = true;
                    break;
                case ',':
                    record.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    break;
                case '\r':
                    break; // swallow; the '\n' closes the record
                case '\n':
                    record.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    records.Add(record);
                    record = new List<string>();
                    break;
                default:
                    field.Append(ch);
                    fieldStarted = true;
                    break;
            }
        }

        // Trailing field/record (no terminating newline).
        if (field.Length > 0 || fieldStarted || record.Count > 0)
        {
            record.Add(field.ToString());
            records.Add(record);
        }
        return records;
    }
}
