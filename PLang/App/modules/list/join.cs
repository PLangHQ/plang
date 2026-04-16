namespace App.modules.list;

[Action("join")]
public partial class Join : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    [Default(",")]
    public partial Data.@this<string> Separator { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        var strings = new List<string>();

        foreach (var (_, item) in data.EnumerateItems())
            strings.Add(item.Value?.ToString() ?? "");

        var result = string.Join(Separator.Value!, strings);
        return Task.FromResult(Data(result, App.Data.Type.String));
    }
}
