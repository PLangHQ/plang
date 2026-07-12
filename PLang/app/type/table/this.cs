namespace app.type.table;

/// <summary>
/// PLang <c>table</c> value — a grid of rows and columns with named headers.
/// <c>table</c> names tabular data <em>by shape</em> (rows navigated by index,
/// cells by column name); <c>csv</c> and <c>xlsx</c> are encodings of that shape
/// (the reader dispatches on <c>(table, csv)</c> / <c>(table, xlsx)</c>, and the
/// encoding rides as the <see cref="Kind"/>). This is the sibling of <c>dict</c>
/// (a tree navigated by key) — grouping csv and xlsx under one type is what lets
/// a renderer draw a grid by dispatching on <c>type=table</c> alone.
///
/// <para>The grid is held column-named: each row is a dictionary keyed by the
/// header, so navigation reads naturally — <c>%t.rows%</c> is the row list,
/// <c>%t.rows[0]["amount"]%</c> a cell, <c>%t.rows.count%</c> the height. The
/// header order is preserved on <see cref="Headers"/> so a re-render keeps column
/// order. <c>foreach %t%</c> iterates the rows directly.</para>
/// </summary>
// TODO (not built yet): per-column formatting. Let a goal declare a column's
// plang type/format —
//     - format of %t["amount"] is currency with 0 decimal points, %t["created"] as datetime
// — and apply it as the grid enumerates, so each cell materialises as the right
// plang type (number/currency, datetime) instead of the raw string. When added,
// EnumerateItems/Navigate would type each cell through the per-column format
// rather than letting it lift to its default (text/number) type.
public sealed class @this : global::app.type.item.@this
{
    /// <summary>Column headers in source order.</summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>Rows, each keyed by header — the navigation surface (<c>%t.rows[0]["amount"]%</c>).</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; }

    /// <summary>Row count — the grid's height.</summary>
    public int RowCount => Rows.Count;

    /// <summary>Column count — the grid's width.</summary>
    public int ColumnCount => Headers.Count;

    /// <summary>The encoding the grid was read from (<c>csv</c>, <c>xlsx</c>) —
    /// the table's kind, carried so <c>%t!type%</c> reports <c>{table, csv}</c>.</summary>
    public string? Kind { get; }

    public @this(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, string? kind = null)
    {
        Headers = headers;
        Rows = rows;
        Kind = kind;
    }

    /// <summary>type = <c>table</c>; kind = the encoding it was read from (csv/xlsx).</summary>
    protected internal override global::app.type.@this Type => new("table", Kind);

    /// <summary>
    /// Navigate <c>rows</c> (the row list) and <c>headers</c> (the column names).
    /// Count, indexing and cell access fall out of normal list/dict navigation
    /// (<c>%t.rows[0]["amount"]%</c>, <c>%t.rows.count%</c>, <c>%t.headers.count%</c>).
    /// </summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(
        global::app.data.@this parent, string key)
        => System.Threading.Tasks.ValueTask.FromResult(key.ToLowerInvariant() switch
        {
            "rows" => new global::app.data.@this("rows", Rows, parent: parent),
            "headers" => new global::app.data.@this("headers", Headers, parent: parent),
            _ => parent.Context.NotFound(key),
        });

    /// <summary><c>foreach %t%</c> iterates the rows — each row a dict keyed by header.</summary>
    public override System.Collections.Generic.IEnumerable<(global::app.data.@this key, global::app.data.@this value)>
        EnumerateItems(global::app.actor.context.@this? context)
    {
        int i = 0;
        foreach (var row in Rows)
            yield return (new global::app.data.@this("", i++, context: context),
                          new global::app.data.@this("", row, context: context));
    }
}
