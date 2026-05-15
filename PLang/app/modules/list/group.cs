using app.Variables;

namespace app.modules.list;

[System.ComponentModel.Description("Group list items by a property key, returning [{key, steps}] buckets")]
[Action("group")]
public partial class Group : IContext
{
    public partial Data.@this<Variable> ListName { get; init; }
    [IsNotNull]
    public partial Data.@this<string> Key { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName.Value);
        var key = Key.Value!;

        var groups = new Dictionary<string, List<object?>>();
        foreach (var (_, item) in data.EnumerateItems())
        {
            var keyData = item.GetChild(key);
            var keyValue = keyData.IsInitialized ? keyData.Value?.ToString() ?? "" : "";
            if (!groups.TryGetValue(keyValue, out var group))
            {
                group = new List<object?>();
                groups[keyValue] = group;
            }
            group.Add(item.Value);
        }

        var result = groups.Select(g => new Dictionary<string, object?>
        {
            ["key"] = g.Key,
            ["steps"] = g.Value
        }).ToList();

        return Task.FromResult(Data(result, app.Data.Type.FromName("list")));
    }
}
