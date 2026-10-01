using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.where;

/// <summary>
/// The where part: keeps the rows its condition holds for. The condition is written as
/// <c>{field, op, value}</c>, or <c>{and: [...]}</c> / <c>{or: [...]}</c> over conditions — a list of conditions
/// is all of them.
/// </summary>
public sealed class @this : part.@this
{
    private readonly condition.@this _condition;

    private @this(condition.@this condition) => _condition = condition;

    /// <summary>The query's <paramref name="where"/>: its condition; one that doesn't read is why on <c>data</c>.</summary>
    internal static @this? Create(Data where, Data data, global::app.actor.context.@this context)
        => condition.@this.Create(where, data, context) is { } kept ? new(kept) : null;

    internal override int Rank => 0;

    internal override async System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<part.@this> rest,
        global::app.actor.context.@this context)
        => await Next(await _condition.Keep(rows, context), rest, context);

    internal override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
        => _condition.Output(writer, mode, context);
}
