using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.part;

/// <summary>
/// One part of a query — where, group, distinct or order. A part applies itself to the rows it is handed, then
/// hands what it answers to the parts after it; the query only puts them in order.
/// </summary>
public abstract class @this
{
    /// <summary>The part's name, as the query writes it: <c>where</c>, <c>group</c>, <c>distinct</c>, <c>order</c>.</summary>
    public abstract string Name { get; }

    /// <summary>Its place in SQL's order: where, group, distinct, order.</summary>
    internal abstract int Rank { get; }

    /// <summary>What this part answers for <paramref name="rows"/>, with <paramref name="rest"/> applied to it
    /// after. A new list; <paramref name="rows"/> is unchanged.</summary>
    internal abstract System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<@this> rest,
        global::app.actor.context.@this context);

    /// <summary>Writes the part's value — what follows its name in the query's dict.</summary>
    internal abstract System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context);

    /// <summary>The parts after this one, applied to what it <paramref name="answered"/>; an error is the answer,
    /// named by this part.</summary>
    protected async System.Threading.Tasks.Task<Data> Next(Data answered, IReadOnlyList<@this> rest,
        global::app.actor.context.@this context)
    {
        if (!answered.Success) return Named(answered, context);
        if (rest.Count == 0) return answered;
        return await answered.Use<List>(rows => rest[0].Apply(rows, rest.Skip(1).ToList(), context));
    }

    /// <summary>A failure, said as this part's: <c>where: No item has a field 'agee' …</c>.</summary>
    protected Data Named(Data failed, global::app.actor.context.@this context)
    {
        if (failed.Success || failed.Error is not { } error) return failed;
        return context.Error(new global::app.error.Error($"{Name}: {error.Message}", error.Key, error.StatusCode) { list = [error] });
    }
}
