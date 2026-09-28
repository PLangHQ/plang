namespace app.module.list;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<global::app.type.item.number.@this> AtIndex { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await Value.Given(value => ListName.Use(name => name.Change<app.type.item.list.@this>(Context, list => AtIndex.Use(at => list.Remove(value, at, Context))))));
}
