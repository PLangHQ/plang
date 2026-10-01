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

    /// <summary>The where of <paramref name="written"/>; one that doesn't read is the query's error, naming
    /// this part.</summary>
    internal @this(Data written, global::app.actor.context.@this context) => _condition = Condition(written.Peek(), context);

    internal override int Rank => 0;

    internal override async System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<part.@this> rest,
        global::app.actor.context.@this context)
        => await Next(await _condition.Keep(rows, context), rest, context);

    internal override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
        => _condition.Output(writer, mode, context);

    // The condition a value is written as: a comparison, and/or over conditions, or a list of them (and).
    private condition.@this Condition(object? written, global::app.actor.context.@this context)
    {
        if (written is List conditions)
            return new condition.@and.@this(Conditions(conditions, "a list of conditions", context));
        if (written is not global::app.type.item.dict.@this dict)
            throw Refused($"a condition is {{field, op, value}}, or {{and: [...]}} or {{or: [...]}} — not {Kind(written)}");
        if (dict.Get("and", context) is { } all)
            return new condition.@and.@this(Conditions(all.Peek(), "and", context));
        if (dict.Get("or", context) is { } any)
            return new condition.@or.@this(Conditions(any.Peek(), "or", context));
        if (dict.Get("field", context)?.Peek()?.ToString() is not { Length: > 0 } field)
            throw Refused("a condition names its field: {field, op, value}");
        var op = dict.Get("op", context)?.Peek()?.ToString() ?? "==";
        global::app.data.Operator @operator;
        try { @operator = new global::app.data.Operator(op); }
        catch (System.ArgumentException ex) { throw Refused(ex.Message); }
        return new condition.compare.@this(field, @operator, dict.Get("value", context) ?? context.Null("value"));
    }

    // Each condition of an and or an or; there is at least one.
    private List<condition.@this> Conditions(object? written, string joined, global::app.actor.context.@this context)
    {
        if (written is not List conditions || conditions.Count == 0)
            throw Refused($"{joined} holds a list of conditions, at least one");
        return conditions.Items(context).Select(c => Condition(c.Peek(), context)).ToList();
    }

    private string Kind(object? written)
        => (written as global::app.type.item.@this)?.Type.Name ?? written?.GetType().Name ?? "nothing";

    private global::app.error.AppException Refused(string why)
        => new(new global::app.error.Error($"{Name}: {why}", "QueryInvalid", 400));
}
