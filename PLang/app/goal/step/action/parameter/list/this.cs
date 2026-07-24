using Data = global::app.data.@this;
using System.Collections.Generic;
using System.Linq;

namespace app.goal.step.action.parameter.list;

/// <summary>
/// The parameter NODE — an action's parameters (<c>action.Parameter</c>) and defaults
/// (<c>action.Default</c>), a plang list value whose rows ARE <see cref="Data"/> (the self-describing
/// <c>{name,type,value}</c> envelope — the base list's value face already IS Data rows, so no element
/// type-tag). PROGRAM STRUCTURE: born context-free (the graph is shared across runs), it stores no
/// context. Materialized by the action serializer reader row by row, so a stored parameter list never
/// round-trips through generic reflection. Twin of <see cref="app.goal.step.action.list.@this"/>.
/// </summary>
public sealed class @this : global::app.type.item.list.@this
{
    public @this() : base(new List<object?>()) { }
    public @this(IReadOnlyList<Data> rows) : base(new List<object?>(rows.Cast<object?>())) { }
    // Value→slot materialization: adopt the rows a generic list reader produced.
    public @this(global::app.type.item.list.@this source) : base(source) { }

    /// <summary>Reads the node from a raw wire "parameters" slot — a native <c>list</c> of
    /// <c>Data</c>-wrapped <c>{name, value}</c> dict entries (collections-are-data), or the CLR
    /// <c>IEnumerable</c> of <c>IDictionary</c> shape. Each entry becomes a named Data row still
    /// holding its <c>%var%</c>/literal/container form — a goal call shares the caller's scope, so
    /// resolution happens when the step reads the name, not here.</summary>
    public @this(object? slot, global::app.actor.context.@this context) : base(Rows(slot, context)) { }

    private static List<object?> Rows(object? slot, global::app.actor.context.@this context)
    {
        var rows = new List<object?>();
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
                    rows.Add(new Data(nd.Get("name")?.Peek()?.ToString() ?? "",
                                      nd.Get("value")?.Peek(), context: context));
                    break;
                case IDictionary<string, object?> id:
                    rows.Add(new Data(
                        id.TryGetValue("name", out var en) ? en?.ToString() ?? "" : "",
                        id.TryGetValue("value", out var ev) ? ev : null, context: context));
                    break;
            }
        }
        return rows;
    }

    // Construction sugar — a raw row list becomes the node (inline C#, builder assembly, tests).
    public static implicit operator @this(List<Data> rows) => new((IReadOnlyList<Data>)rows);

    /// <summary>Clone/render keep this concrete node type, context-free.</summary>
    protected override global::app.type.item.list.@this Empty() => new @this();

    /// <summary>The parameter row (a <see cref="Data"/> envelope) at <paramref name="i"/> — the value
    /// face is Data, so this is public (unlike action.list/step.list, whose typed element face is
    /// internal). Build/validation read a row's Name/Value off it.</summary>
    public Data this[int i] => At(i) ?? throw new System.IndexOutOfRangeException(
        $"index {i} is out of range for a parameter list of {CountRaw}");
}
