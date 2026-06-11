using app.variable;

namespace app.module.list;

[Action("join")]
public partial class Join : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    [Default(",")]
    public partial data.@this<global::app.type.text.@this> Separator { get; init; }

    public async Task<data.@this<global::app.type.text.@this>> Run()
    {
        var data = await Context.Variable.Get(await ListName.Value());
        var strings = new List<string>();

        foreach (var (_, item) in data.EnumerateItems())
            strings.Add((await item.Value())?.ToString() ?? "");

        var result = string.Join((await Separator.Value())!.Clr<string>()!, strings);
        return global::app.data.@this<global::app.type.text.@this>.Ok(result, app.type.@this.String);
    }
}
