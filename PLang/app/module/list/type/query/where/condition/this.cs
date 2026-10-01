using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.where.condition;

/// <summary>
/// What a where keeps: a comparison of one field (<c>age &gt; 20</c>), or several joined by <c>and</c> or <c>or</c>.
/// Each keeps from the rows by itself; the list and its items do the comparing (<c>list.Where</c>).
/// </summary>
public abstract class @this
{
    /// <summary>The rows this condition keeps, in their order — a new list.</summary>
    internal abstract System.Threading.Tasks.Task<Data> Keep(List rows, global::app.actor.context.@this context);

    /// <summary>Writes the condition as the query's dict holds it.</summary>
    internal abstract System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context);
}
