namespace app.module.list;

[Action("split")]
public partial class Split : IContext
{
    public partial data.@this<global::app.type.item.text.@this> Value { get; init; }
    [Default(",")]
    public partial data.@this<global::app.type.item.text.@this> Separator { get; init; }
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> RemoveEmpty { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await Value.Use(text => text.Split(Separator, RemoveEmpty, Context)));
}
