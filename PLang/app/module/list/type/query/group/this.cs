using Data = global::app.data.@this;
using List = global::app.type.item.list.@this;

namespace app.module.list.type.query.group;

/// <summary>
/// The group part: the rows grouped by one field — a list of <c>{key, items}</c> (<c>list.Group</c>). The parts
/// after a group apply inside each group's items: an order sorts each group's items, a distinct drops repeats
/// within a group.
/// </summary>
public sealed class @this : part.@this
{
    private readonly global::app.type.item.text.@this _field;

    private @this(global::app.type.item.text.@this field) => _field = field;

    /// <summary>The group of <paramref name="written"/>: the field to group by; none named is why on <c>data</c>.</summary>
    internal static @this? Create(Data written, Data data, global::app.actor.context.@this context)
    {
        if (written.Peek()?.ToString() is { Length: > 0 } field) return new(field);
        data.Fail(new global::app.error.Error("group: names the field to group by — group: \"name\"", "QueryInvalid", 400));
        return null;
    }

    internal override int Rank => 1;

    // each group's items, with the parts after the group applied to them; a part's failure is named by that part
    internal override System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<part.@this> rest,
        global::app.actor.context.@this context)
        => rows.Group(_field,
            items => rest.Count == 0 ? System.Threading.Tasks.Task.FromResult<Data>(context.Ok(items))
                : rest[0].Apply(items, rest.Skip(1).ToList(), context), context);

    internal override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
    {
        writer.String(_field.ToString());
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
