using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.distinct;

/// <summary>The distinct part: the rows with their repeats dropped, the first of each kept (<c>list.Unique</c>).
/// Written <c>distinct: true</c>; <c>distinct: false</c> keeps the repeats — the rows go on as they are.</summary>
public sealed class @this : part.@this
{
    private readonly bool _on;

    /// <summary>The distinct of <paramref name="written"/>: whether repeats are dropped.</summary>
    internal @this(Data written, global::app.actor.context.@this context)
        => _on = written.Peek() is global::app.type.item.@bool.@this { Value: true };

    internal override int Rank => 2;

    internal override async System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<part.@this> rest,
        global::app.actor.context.@this context)
        => await Next(_on ? await rows.Unique(context) : context.Ok(rows), rest, context);

    internal override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
    {
        writer.Bool(_on);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
