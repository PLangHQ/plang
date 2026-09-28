namespace app.module.list;

[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await ListName.Use(name => name.Change<app.type.item.list.@this>(Context,
            list => Task.FromResult<data.@this>(Context.Ok(list.Reverse())))));
}
