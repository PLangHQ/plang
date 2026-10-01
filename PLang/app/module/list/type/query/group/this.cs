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

    /// <summary>The group of <paramref name="written"/>: the field to group by.</summary>
    internal @this(Data written)
    {
        if (written.Peek()?.ToString() is not { Length: > 0 } field)
            throw new global::app.error.AppException(new global::app.error.Error(
                $"{Name}: names the field to group by — group: \"name\"", "QueryInvalid", 400));
        _field = field;
    }

    public override string Name => "group";

    internal override int Rank => 1;

    internal override async System.Threading.Tasks.Task<Data> Apply(List rows, IReadOnlyList<part.@this> rest,
        global::app.actor.context.@this context)
    {
        var grouped = await rows.Group(_field, context);
        if (!grouped.Success || rest.Count == 0) return Named(grouped, context);
        return await grouped.Use<List>(async groups =>
        {
            var each = new List<Dictionary<string, object?>>();
            foreach (var group in groups.Items(context))
            {
                var applied = await (await group.Get("items")).Use<List>(items =>
                    rest[0].Apply(items, rest.Skip(1).ToList(), context));
                if (!applied.Success) return applied;
                each.Add(new() { ["key"] = await group.Get("key"), ["items"] = applied });
            }
            return await context.App.type.list["list"].Create(each, context);
        });
    }

    internal override System.Threading.Tasks.ValueTask Output(global::app.type.format.IWriter writer, global::app.View mode,
        global::app.actor.context.@this context)
    {
        writer.String(_field.ToString());
        return System.Threading.Tasks.ValueTask.CompletedTask;
    }
}
