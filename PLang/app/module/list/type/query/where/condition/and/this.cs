using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.where.condition.@and;

/// <summary>Conditions that must all hold: each keeps from what the one before it kept.</summary>
public sealed class @this : condition.@this
{
    private readonly IReadOnlyList<condition.@this> _condition;

    internal @this(IReadOnlyList<condition.@this> condition) => _condition = condition;

    internal override async System.Threading.Tasks.Task<Data> Keep(List rows, global::app.actor.context.@this context)
    {
        Data kept = context.Ok(rows);
        foreach (var condition in _condition)
        {
            kept = await kept.Use<List>(left => condition.Keep(left, context));
            if (!kept.Success) return kept;
        }
        return kept;
    }

    internal override async System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
    {
        writer.BeginObject();
        writer.Name("and");
        writer.BeginArray(_condition.Count);
        foreach (var condition in _condition) await condition.Output(writer, mode, context);
        writer.EndArray();
        writer.EndObject();
    }
}
