using App.Variables;

namespace App.modules.list;

[System.ComponentModel.Description("Concatenate all list items into a single string, separated by Separator (default comma)")]
[Action("join")]
public partial class Join : IContext
{
    public partial Data.@this<Variable> ListName { get; init; }
    [Default(",")]
    public partial Data.@this<string> Separator { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var strings = new List<string>();

        foreach (var (_, item) in data.EnumerateItems())
            strings.Add(item.Value?.ToString() ?? "");

        var result = string.Join(Separator.Value!, strings);
        return Task.FromResult(Data(result, App.Data.Type.String));
    }
}
