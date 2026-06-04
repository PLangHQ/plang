namespace app.type.table;

/// <summary>
/// PLang <c>table</c> value — a grid of rows and columns with named headers.
/// <c>table</c> names tabular data <em>by shape</em> (rows navigated by index,
/// cells by column name); <c>csv</c> and <c>xlsx</c> are encodings of that shape
/// (the reader dispatches on <c>(table, csv)</c> / <c>(table, xlsx)</c>). This is
/// the sibling of <c>object</c> (a tree navigated by key) — grouping csv and xlsx
/// under one type is what lets a renderer draw a grid by dispatching on
/// <c>type=table</c> alone.
///
/// <para>The grid is held column-named: each row is a dictionary keyed by the
/// header, so navigation reads naturally — <c>%t.rows%</c> is the row list,
/// <c>%t.rows[0].name%</c> a cell. The header order is preserved separately on
/// <see cref="Headers"/> so a re-render keeps column order.</para>
/// </summary>
public sealed class @this
{
    /// <summary>Column headers in source order.</summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>Rows, each keyed by header — the navigation surface (<c>%t.rows[0].name%</c>).</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; }

    /// <summary>Row count — the grid's height.</summary>
    public int RowCount => Rows.Count;

    /// <summary>Column count — the grid's width.</summary>
    public int ColumnCount => Headers.Count;

    public @this(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        Headers = headers;
        Rows = rows;
    }
}
