using app.variable;

namespace app.module.list;

[Action("join")]
public partial class Join : IContext
{
    public partial data.@this<app.variable.@this> ListName { get; init; }
    [Default(",")]
    public partial data.@this<global::app.type.text.@this> Separator { get; init; }

    public Task<data.@this<global::app.type.text.@this>> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
        var strings = new List<string>();

        foreach (var (_, item) in data.EnumerateItems())
            strings.Add(item.Value?.ToString() ?? "");

        var result = string.Join(Separator.Value!, strings);
        return Task.FromResult(global::app.data.@this<global::app.type.text.@this>.Ok(result, app.type.@this.String));
    }
}
