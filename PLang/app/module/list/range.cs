namespace app.module.list;

[Action("range")]
public partial class Range : IContext
{
    public partial data.@this<global::app.type.item.number.@this> From { get; init; }
    public partial data.@this<global::app.type.item.number.@this> To { get; init; }
    [Default(1)]
    public partial data.@this<global::app.type.item.number.@this> Step { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await From.Use(from => from.Range(To, Step, Context)));
}
