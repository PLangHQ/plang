using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.where;

/// <summary>
/// The where part: keeps the rows its condition holds for. The condition is written as
/// <c>{field, op, value}</c>, or <c>{and: [...]}</c> / <c>{or: [...]}</c> over conditions — a list of conditions
/// is all of them.
/// </summary>
public sealed class @this : query.@this
{
    private readonly condition.@this _condition;

    private @this(condition.@this condition, global::app.type.@this type) : base(type) => _condition = condition;

    /// <summary>The query's <paramref name="where"/>: its condition; one that doesn't read is why on <c>data</c>. Born
    /// holding the app's <c>{query, where}</c>.</summary>
    internal static @this? Create(Data where, Data data, global::app.actor.context.@this context)
        => condition.@this.Create(where, data, context) is { } predicate ? new(predicate, context.App.type.list[typeof(@this)]) : null;

    internal override int Rank => 0;

    internal override async System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<query.@this> rest,
        global::app.actor.context.@this context)
        => await Next(await _condition.Keep(rows, context), rest, context);

    protected internal override System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
        => _condition.Output(writer);
}
