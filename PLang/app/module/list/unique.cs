namespace app.module.list;

[Action("unique")]
public partial class Unique : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await ListName.Use(name => name.Use<app.type.item.list.@this>(Context, list => list.Unique(Context))));
}
