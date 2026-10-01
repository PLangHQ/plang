using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.where.condition.@or;

/// <summary>Conditions of which one must hold: each keeps from the rows, and the rows' elements any of them kept
/// are the answer, in the rows' order.</summary>
public sealed class @this : condition.@this
{
    private readonly IReadOnlyList<condition.@this> _condition;

    private @this(IReadOnlyList<condition.@this> condition) => _condition = condition;

    /// <summary>The <paramref name="conditions"/>, one to hold; null with why on <c>data</c>.</summary>
    internal static @this? Create(Data conditions, Data data, global::app.actor.context.@this context)
        => Read(conditions, "or", data, context) is { } either ? new(either) : null;

    internal override async System.Threading.Tasks.Task<Data> Keep(List rows, global::app.actor.context.@this context)
    {
        // the rows' elements held once, as Data: a condition keeps these very elements, so one kept is known by
        // reference
        var elements = rows.Items(context).ToList();
        var held = new List(elements);
        var kept = new HashSet<Data>(ReferenceEqualityComparer.Instance);
        foreach (var condition in _condition)
        {
            var answered = await condition.Keep(held, context);
            if (!answered.Success) return answered;
            var one = await answered.Use<List>(left =>
            {
                foreach (var element in left.Items(context)) kept.Add(element);
                return System.Threading.Tasks.Task.FromResult(answered);
            });
            if (!one.Success) return one;
        }
        return await context.App.type.list["list"].Create(elements.Where(kept.Contains).ToList(), context);
    }

    internal override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
    {
        writer.BeginObject();
        writer.Name("or");
        writer.BeginArray(_condition.Count);
        foreach (var condition in _condition) await condition.Output(writer, mode, context);
        writer.EndArray();
        writer.EndObject();
    }
}
