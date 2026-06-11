using app.variable;

namespace app.module.list;

[Action("split")]
public partial class Split : IContext
{
    public partial data.@this<global::app.type.text.@this> Value { get; init; }
    [Default(",")]
    public partial data.@this<global::app.type.text.@this> Separator { get; init; }
    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> RemoveEmpty { get; init; }

    public async Task<data.@this<type.list>> Run()
    {
        var options = (await RemoveEmpty.Value())?.Value == true
            ? StringSplitOptions.RemoveEmptyEntries
            : StringSplitOptions.None;

        var parts = (await Value.Value())!.Value.Split(new[] { (await Separator.Value())!.Value }, options);
        var list = new app.type.list.@this { Context = Context };
        foreach (var part in parts)
            list.Add(new global::app.data.@this("", part));

        return global::app.data.@this<type.list>.Ok(
            new type.list { count = list.CountRaw, value = list }, app.type.@this.FromName("list"));
    }
}
