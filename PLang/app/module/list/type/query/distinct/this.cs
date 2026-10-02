using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.distinct;

/// <summary>The distinct part: the rows with their repeats dropped, the first of each kept (<c>list.Unique</c>).
/// Written <c>distinct: true</c>; <c>distinct: false</c> keeps the repeats — the rows go on as they are.</summary>
public sealed class @this : query.@this
{
    private readonly global::app.type.item.@bool.@this _on;

    private @this(global::app.type.item.@bool.@this on) => _on = on;

    /// <summary>The query's <paramref name="distinct"/>: whether repeats are dropped — a bool, as bool reads one;
    /// what doesn't read as a bool is why on <c>data</c>.</summary>
    internal static @this? Create(Data distinct, Data data, global::app.actor.context.@this context)
        => global::app.type.item.@bool.@this.Create(distinct.Peek(), null, data) is { } on ? new(on) : null;

    internal override int Rank => 2;

    internal override async System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<query.@this> rest,
        global::app.actor.context.@this context)
        => await Next(_on.Value ? await rows.Unique(context) : context.Ok(rows), rest, context);

    public override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this? context)
    {
        _on.Write(writer);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
