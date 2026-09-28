namespace app.module.action.list;

[Action("contains")]
public partial class Contains : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<global::app.type.item.@bool.@this>> Start() => data.@this<global::app.type.item.@bool.@this>.From(
        await Value.Given(value => ListName.Use(name => name.Use<app.type.item.list.@this>(Context,
            async list => Context.Ok<global::app.type.item.@bool.@this>(await list.Contains(value))))));
}
