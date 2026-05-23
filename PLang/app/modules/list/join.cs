using app.variables;

namespace app.modules.list;

[System.ComponentModel.Description("Concatenate all list items into a single string, separated by Separator (default comma)")]
[Action("join")]
public partial class Join : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    [Default(",")]
    public partial data.@this<string> Separator { get; init; }

    public Task<data.@this<string>> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var strings = new List<string>();

        foreach (var (_, item) in data.EnumerateItems())
            strings.Add(item.Value?.ToString() ?? "");

        var result = string.Join(Separator.Value!, strings);
        return Task.FromResult(global::app.data.@this<string>.Ok(result, app.data.type.String));
    }
}
