namespace app.module.action.list;

[Action("join")]
public partial class Join : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    [Default(",")]
    public partial data.@this<global::app.type.item.text.@this> Separator { get; init; }

    public async Task<data.@this<global::app.type.item.text.@this>> Start() => data.@this<global::app.type.item.text.@this>.From(
        await ListName.Use(name => name.Use<app.type.item.list.@this>(Context, list => list.Join(Separator, Context))));
}
