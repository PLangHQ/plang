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

    /// <summary>The where of <paramref name="written"/>; one that doesn't read is why on <c>data</c>, naming this part.</summary>
    internal static @this? Create(Data written, Data data, global::app.actor.context.@this context)
        => Condition(written.Peek(), data, context) is { } condition ? new(condition) : null;

    internal override int Rank => 0;

    internal override async System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<part.@this> rest,
        global::app.actor.context.@this context)
        => await Next(await _condition.Keep(rows, context), rest, context);

    internal override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
        => _condition.Output(writer, mode, context);

    // The condition a value is written as: a comparison, and/or over conditions, or a list of them (and); null with why
    // on data when it doesn't read.
    private static condition.@this? Condition(object? written, Data data, global::app.actor.context.@this context)
    {
        if (written is List conditions)
            return Conditions(conditions, "a list of conditions", data, context) is { } listed ? new condition.@and.@this(listed) : null;
        if (written is not global::app.type.item.dict.@this dict)
            return Refused(data, $"a condition is {{field, op, value}}, or {{and: [...]}} or {{or: [...]}} — not {Kind(written)}");
        if (dict.Get("and", context) is { } all)
            return Conditions(all.Peek(), "and", data, context) is { } each ? new condition.@and.@this(each) : null;
        if (dict.Get("or", context) is { } any)
            return Conditions(any.Peek(), "or", data, context) is { } either ? new condition.@or.@this(either) : null;
        if (dict.Get("field", context)?.Peek()?.ToString() is not { Length: > 0 } field)
            return Refused(data, "a condition names its field: {field, op, value}");
        var op = dict.Get("op", context)?.Peek()?.ToString() ?? "==";
        var known = global::app.data.Operator.Choices(context);
        if (!known.Contains(op, StringComparer.OrdinalIgnoreCase))
            return Refused(data, $"Unsupported operator: '{op}'. Valid: {string.Join(", ", known)}");
        return new condition.compare.@this(field, new global::app.data.Operator(op), dict.Get("value", context) ?? context.Null("value"));
    }

    // Each condition of an and or an or; there is at least one. Null with why on data when one doesn't read.
    private static List<condition.@this>? Conditions(object? written, string joined, Data data, global::app.actor.context.@this context)
    {
        if (written is not List conditions || conditions.Count == 0)
        {
            Refused(data, $"{joined} holds a list of conditions, at least one");
            return null;
        }
        var each = new List<condition.@this>();
        foreach (var c in conditions.Items(context))
        {
            if (Condition(c.Peek(), data, context) is not { } read) return null;
            each.Add(read);
        }
        return each;
    }

    private static string Kind(object? written)
        => (written as global::app.type.item.@this)?.Type.Name ?? written?.GetType().Name ?? "nothing";

    // why the where doesn't read, on data, said as the where's
    private static condition.@this? Refused(Data data, string why)
    {
        data.Fail(new global::app.error.Error($"where: {why}", "QueryInvalid", 400));
        return null;
    }
}
