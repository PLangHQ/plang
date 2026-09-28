namespace app.module.action.list;

[Action("group")]
public partial class Group : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Key { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await ListName.Use(name => name.Use<app.type.item.list.@this>(Context, list => list.Group(Key, Context))));
}
