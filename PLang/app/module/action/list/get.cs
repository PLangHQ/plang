namespace app.module.action.list;

[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    public partial data.@this<global::app.type.item.number.@this> Index { get; init; }

    public async Task<data.@this> Start()
    {
        var data = await (await ListName.Value())!.Start(Context);
        var item = await data.Get($"[{(await Index.Value())}]");

        if (!item.IsInitialized)
            return Context.Error(
                new app.error.ValidationError($"Index {(await Index.Value())} out of range for '{(await ListName.Value())}'"));

        return Context.Ok((await item.Value()));
    }
}
