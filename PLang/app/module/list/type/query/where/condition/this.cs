using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.where.condition;

/// <summary>
/// What a where keeps: a comparison of one field (<c>age &gt; 20</c>), or several joined by <c>and</c> or <c>or</c>.
/// Each keeps from the rows by itself; the list and its items do the comparing (<c>list.Where</c>).
/// </summary>
public abstract class @this
{
    /// <summary>What <paramref name="condition"/> is: a comparison (<c>{field, op, value}</c>), <c>{and: [...]}</c>,
    /// <c>{or: [...]}</c>, or a list of conditions (all of them). One that doesn't read is why on <c>data</c>.</summary>
    internal static @this? Create(Data condition, Data data, global::app.actor.context.@this context)
    {
        if (condition.Peek() is List)
            return Read(condition, "a list of conditions", data, context) is { } all ? new @and.@this(all) : null;
        if (condition.Peek() is not global::app.type.item.dict.@this comparison)
            return Refused<@this>(data, $"a condition is {{field, op, value}}, or {{and: [...]}} or {{or: [...]}} — not {condition.Type.Name}");
        if (comparison.Get("and", context) is { } each) return @and.@this.Create(each, data, context);
        if (comparison.Get("or", context) is { } either) return @or.@this.Create(either, data, context);
        return compare.@this.Create(comparison, data, context);
    }

    /// <summary>Each of <paramref name="conditions"/>, at least one, for a condition that joins them
    /// (<paramref name="joined"/>: <c>and</c>, <c>or</c>). Null with why on <c>data</c> when one doesn't read.</summary>
    protected static IReadOnlyList<@this>? Read(Data conditions, string joined, Data data, global::app.actor.context.@this context)
    {
        if (conditions.Peek() is not List listed || listed.Count == 0)
            return Refused<IReadOnlyList<@this>>(data, $"{joined} holds a list of conditions, at least one");
        var each = new List<@this>();
        foreach (var condition in listed.Items(context))
        {
            if (Create(condition, data, context) is not { } read) return null;
            each.Add(read);
        }
        return each;
    }

    /// <summary>Why the condition doesn't read, on <c>data</c>; the query names the part it belongs to.</summary>
    protected static T? Refused<T>(Data data, string why) where T : class
    {
        data.Fail(new global::app.error.Error(why, "QueryInvalid", 400));
        return null;
    }

    /// <summary>The rows this condition keeps, in their order — a new list.</summary>
    internal abstract System.Threading.Tasks.Task<Data> Keep(List rows, global::app.actor.context.@this context);

    /// <summary>Writes the condition as the query's dict holds it.</summary>
    internal abstract System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context);
}
