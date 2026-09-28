namespace app.module.list;

[Action("first")]
public partial class First : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }

    public Task<data.@this> Start() => ListName.Use(name => name.Use<app.type.item.list.@this>(Context,
        list => Task.FromResult(list.First(Context) ?? Context.Ok())));
}
