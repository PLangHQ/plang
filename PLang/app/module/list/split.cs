namespace app.module.list;

[Action("split")]
public partial class Split : IContext
{
    public partial data.@this<global::app.type.item.text.@this> Value { get; init; }
    [Default(",")]
    public partial data.@this<global::app.type.item.separator.@this> Separator { get; init; }
    /// <summary>What becomes of the empty pieces — kept, unless the step drops them.</summary>
    [Default(global::app.type.item.text.empty.keep)]
    public partial data.@this<global::app.type.item.choice.@this<global::app.type.item.text.empty>> Empty { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Start() => data.@this<app.type.item.list.@this>.From(
        await Value.Use(text => text.Split(Separator, Empty, Context)));
}
