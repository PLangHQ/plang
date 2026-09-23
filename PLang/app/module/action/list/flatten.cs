using app.variable;

namespace app.module.action.list;

[Action("flatten")]
public partial class Flatten : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Run()
    {
        var name = await ListName.Value();
        if (await (await Context.Variable.Get(name)).Value() is not app.type.item.list.@this nl)
            return Context.Error<app.type.item.list.@this>(
                new app.error.ValidationError($"Variable '{name}' is not a list"));

        var flat = new app.type.item.list.@this(Context);
        await FlattenNative(nl, flat);
        return Context.Ok(flat);
    }

    // Flatten a native list: a nested-list element's elements are lifted; any other
    // element Data is kept as-is (its own type-tag preserved). Each element materializes
    // through its own door — a nested list surfaces as a native list, so this single arm
    // covers every case.
    private static async Task FlattenNative(app.type.item.list.@this source, app.type.item.list.@this target)
    {
        foreach (var item in source.Items)
        {
            if ((await item.Value()) is app.type.item.list.@this nested)
                await FlattenNative(nested, target);
            else
                target.Add(item);
        }
    }
}
