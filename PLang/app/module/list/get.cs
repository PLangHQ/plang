namespace app.module.list;

[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<app.type.item.variable.@this> ListName { get; init; }
    public partial data.@this<global::app.type.item.number.@this> Index { get; init; }

    public Task<data.@this> Start() => ListName.Use(name => name.Use<app.type.item.list.@this>(Context,
        list => Index.Use(at => Task.FromResult(list.At(at, Context)))));
}
