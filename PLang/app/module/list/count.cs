namespace app.module.list;

[Action("count")]
public partial class Count : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }

    public async Task<data.@this<global::app.type.item.number.@this>> Start() => data.@this<global::app.type.item.number.@this>.From(
        await ListName.Use(name => name.Use<app.type.item.list.@this>(Context,
            list => Task.FromResult<data.@this>(Context.Ok<global::app.type.item.number.@this>(list.Count)))));
}
