namespace app.module.list;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    public partial data.@this<global::app.type.item.number.@this> Index { get; init; }
    public partial data.@this Value { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await (Value ?? Context.Null()).Given(value => ListName.Use(name => name.Change<app.type.item.list.@this>(Context,
            list => list.SetAt(Index, value, Context)))));
}
