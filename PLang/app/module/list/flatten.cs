using app.variable;

namespace app.module.list;

[Action("flatten")]
public partial class Flatten : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var nl = app.type.list.@this.FromRaw((await (await Context.Variable.Get((await ListName.Value()))).Value()), Context);
        if (nl == null)
            return Context.Error<type.list>(
                new app.error.ValidationError($"Variable '{(await ListName.Value())}' is not a list"));

        var flat = new app.type.list.@this(Context);
        await FlattenNative(nl, flat);
        return Context.Ok<type.list>(new type.list { count = flat.CountRaw, value = flat }, Context.Type.Create("list"));
    }

    // Flatten a native list: a nested-list element's elements are lifted; any other
    // element Data is kept as-is (its own type-tag preserved). FromRaw has already
    // converted any nested raw lists to native, so this single arm covers every case.
    private static async Task FlattenNative(app.type.list.@this source, app.type.list.@this target)
    {
        foreach (var item in source.Items)
        {
            if ((await item.Value()) is app.type.list.@this nested)
                await FlattenNative(nested, target);
            else
                target.Add(item);
        }
    }
}
