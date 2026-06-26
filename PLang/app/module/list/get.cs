using app.variable;

namespace app.module.list;

[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    public partial data.@this<global::app.type.number.@this> Index { get; init; }

    public async Task<data.@this> Run()
    {
        var data = await Context.Variable.Get((await ListName.Value()));
        var item = await data.GetChild($"[{(await Index.Value())}]");

        if (!item.IsInitialized)
            return Context.Error(
                new app.error.ValidationError($"Index {(await Index.Value())} out of range for '{(await ListName.Value())}'"));

        return Context.Ok((await item.Value()));
    }
}
