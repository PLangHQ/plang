namespace app.module.list;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    public partial data.@this Value { get; init; }
    [Default(-1)]
    public partial data.@this<global::app.type.item.number.@this> At { get; init; }

    // A list is born when the variable holds none (runs adding at once all reach one list); a refused birth or
    // write is the answer. Then the value is added — what the step gave, as it is now: a %variable% the value
    // names is followed here, and variables inside it ({"text": "got %x%"}) are filled here, as `set` fills
    // them — not later against whatever they hold then.
    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await Value.Given(given => ListName.Use(async name =>
        {
            var value = !Value.IsVariable && Value.HasVariable ? await global::app.module.variable.Set.Filled(given, Context) : given;
            if (!value.Success) return value;
            var held = await name.Ensure(() => Context.App.type.list["list"].Create(System.Array.Empty<object?>(), Context), Context);
            if (!held.Success || held.Handled) return held;
            return await name.Change<app.type.item.list.@this>(Context, list => At.Use(at => list.Add(value, at, Context)));
        })));
}
