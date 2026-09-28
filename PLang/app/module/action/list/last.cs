namespace app.module.action.list;

[Action("last")]
public partial class Last : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }

    public Task<data.@this> Start() => ListName.Use(name => name.Use<app.type.item.list.@this>(Context,
        list => Task.FromResult(list.Last(Context) ?? Context.Ok())));
}
