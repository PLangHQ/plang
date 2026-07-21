using Data = global::app.data.@this;
using System.Collections.Generic;

namespace app.goal.step.action.parameter.list;

/// <summary>
/// The parameter NODE — an action's parameters (<c>action.Parameter</c>) and defaults
/// (<c>action.Default</c>). Rows ARE <see cref="Data"/> (the self-describing <c>{name,type,value}</c>
/// envelope). Replaces the naked <c>List&lt;data&gt;</c>: a singular concept-named collection that
/// owns its own <c>Add</c> and read-only surface, mirroring <see cref="app.goal.step.action.list.@this"/>.
/// Materialized by the action serializer reader (row by row through the Data reader), so a stored
/// parameter list never round-trips through generic reflection.
/// </summary>
public sealed class @this : IReadOnlyList<Data>
{
    // A List reused when the caller already has one, wrapped once otherwise; Add is a construction
    // affordance only (the graph is read-only after load).
    private readonly List<Data> _rows;
    public @this(IReadOnlyList<Data> rows) => _rows = rows as List<Data> ?? new List<Data>(rows);
    public @this() => _rows = new List<Data>();

    // Construction sugar — a raw row list becomes the node (inline C#, builder assembly, tests).
    public static implicit operator @this(List<Data> rows) => new(rows);

    public Data this[int i] => _rows[i];
    public IReadOnlyList<Data> list => _rows;
    public int Count => _rows.Count;
    public void Add(Data row) => _rows.Add(row);            // construction only
    public IEnumerator<Data> GetEnumerator() => _rows.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
