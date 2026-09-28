namespace app.module.list;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<global::app.type.item.number.@this> AtIndex { get; init; }

    // A list is born when the variable holds none (runs adding at once all reach one list); a refused birth or
    // write is the answer. Then the value is added — what the step gave, as it is now: a %variable% the value
    // names is followed here, not later against whatever it names then.
    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await Value.Given(value => ListName.Use(async name =>
        {
            var held = await name.Ensure(() => Context.App.type.list["list"].Create(System.Array.Empty<object?>(), Context), Context);
            if (!held.Success || held.Handled) return held;
            return await name.Change<app.type.item.list.@this>(Context, list => list.Add(value, AtIndex, Context));
        })));
}
