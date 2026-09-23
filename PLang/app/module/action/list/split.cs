using app.variable;

namespace app.module.action.list;

[Action("split")]
public partial class Split : IContext
{
    public partial data.@this<global::app.type.item.text.@this> Value { get; init; }
    [Default(",")]
    public partial data.@this<global::app.type.item.text.@this> Separator { get; init; }
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> RemoveEmpty { get; init; }

    public async Task<data.@this<app.type.item.list.@this>> Run()
    {
        var options = await RemoveEmpty.ToBooleanAsync()
            ? StringSplitOptions.RemoveEmptyEntries
            : StringSplitOptions.None;

        var parts = (await Value.Value())!.Clr<string>()!.Split(new[] { (await Separator.Value())!.Clr<string>()! }, options);
        var list = new app.type.item.list.@this(Context);
        foreach (var part in parts)
            list.Add(new global::app.data.@this("", part, context: Context));

        return Context.Ok(list);
    }
}
