using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.order;

/// <summary>
/// The order part: the rows sorted by its keys, the first key first. Written as a field (<c>order: "age"</c>), one
/// key (<c>{field: "age", desc: true}</c>) or a list of keys; a key with no field orders by the elements themselves.
/// </summary>
public sealed class @this : query.@this
{
    private readonly IReadOnlyList<key.@this> _key;

    private @this(IReadOnlyList<key.@this> key) => _key = key;

    /// <summary>The query's <paramref name="order"/>: its keys, in the order they decide; a key that doesn't read
    /// is why on <c>data</c>.</summary>
    internal static @this? Create(Data order, Data data, global::app.actor.context.@this context)
    {
        IEnumerable<Data> each = order.Peek() is List listed ? listed.Items(context) : [order];
        var keys = new List<key.@this>();
        foreach (var one in each)
        {
            if (key.@this.Create(one, data, context) is not { } read) return null;
            keys.Add(read);
        }
        return new(keys);
    }

    internal override int Rank => 3;

    internal override async System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<query.@this> rest,
        global::app.actor.context.@this context)
    {
        // the last key sorts first: the sort keeps the order of equal keys, so each key before it decides over it
        Data sorted = context.Ok(rows);
        foreach (var by in _key.Reverse())
        {
            sorted = await sorted.Use<List>(left => by.Sort(left, context));
            if (!sorted.Success) break;
        }
        return await Next(sorted, rest, context);
    }

    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        writer.BeginArray(_key.Count);
        foreach (var by in _key) by.Output(writer);
        writer.EndArray();
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
