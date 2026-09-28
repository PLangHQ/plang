namespace app.module.action.list;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<global::app.type.item.number.@this> AtIndex { get; init; }

    // A list when the variable holds none (runs adding at once all reach one list), then the value is added.
    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await ListName.Use(async name =>
        {
            await name.Ensure(() => new app.type.item.list.@this(), Context);
            return await name.Change<app.type.item.list.@this>(Context, list => list.Add(Value, AtIndex, Context));
        }));
}
