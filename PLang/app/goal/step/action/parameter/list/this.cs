using Data = global::app.data.@this;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>Reads the node from a raw wire "parameters" slot — a native <c>list</c> of
    /// <c>Data</c>-wrapped <c>{name, value}</c> dict entries (collections-are-data), or the CLR
    /// <c>IEnumerable</c> of <c>IDictionary</c> shape. Each entry becomes a named Data row still
    /// holding its <c>%var%</c>/literal/container form — a goal call shares the caller's scope, so
    /// resolution happens when the step reads the name, not here. An entry that is neither dict shape
    /// is skipped so a malformed slot never silently drops the whole set.</summary>
    public @this(object? slot, global::app.actor.context.@this context)
    {
        _rows = new List<Data>();
        IEnumerable<object?> elements = slot switch
        {
            global::app.type.item.list.@this nativeList => nativeList.Items,
            string => System.Array.Empty<object?>(),
            System.Collections.IEnumerable seq => seq.Cast<object?>(),
            _ => System.Array.Empty<object?>(),
        };
        foreach (var element in elements)
        {
            var entry = element is Data d ? d.Peek() : element;
            switch (entry)
            {
                case global::app.type.item.dict.@this nd:
                    _rows.Add(new Data(nd.Get("name")?.Peek()?.ToString() ?? "",
                                       nd.Get("value")?.Peek(), context: context));
                    break;
                case IDictionary<string, object?> id:
                    _rows.Add(new Data(
                        id.TryGetValue("name", out var en) ? en?.ToString() ?? "" : "",
                        id.TryGetValue("value", out var ev) ? ev : null, context: context));
                    break;
            }
        }
    }

    // Construction sugar — a raw row list becomes the node (inline C#, builder assembly, tests).
    public static implicit operator @this(List<Data> rows) => new(rows);

    public Data this[int i] => _rows[i];
    public IReadOnlyList<Data> list => _rows;
    public int Count => _rows.Count;
    public void Add(Data row) => _rows.Add(row);            // construction only
    public IEnumerator<Data> GetEnumerator() => _rows.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
