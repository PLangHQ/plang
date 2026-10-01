using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.distinct;

/// <summary>The distinct part: the rows with their repeats dropped, the first of each kept (<c>list.Unique</c>).
/// Written <c>distinct: true</c>; <c>false</c> is no part.</summary>
public sealed class @this : part.@this
{
    public override string Name => "distinct";

    internal override int Rank => 2;

    internal override async System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<part.@this> rest,
        global::app.actor.context.@this context)
        => await Next(await rows.Unique(context), rest, context);

    internal override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
    {
        writer.Bool(true);
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
