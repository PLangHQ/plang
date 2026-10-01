namespace app.module.list;

[Action("split")]
public partial class Split : IContext
{
    public partial data.@this<global::app.type.item.text.@this> Value { get; init; }
    [Default(",")]
    public partial data.@this<global::app.type.item.text.@this> Separator { get; init; }
    /// <summary>Whether the empty pieces are kept — true unless the step leaves them out.</summary>
    [Default(true)]
    public partial data.@this<global::app.type.item.@bool.@this> Empty { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await Value.Use(text => text.Split(Separator, Empty, Context)));
}
