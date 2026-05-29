using app.variable;

namespace app.module.list;

[Action("group")]
public partial class Group : IContext
{
    public partial data.@this<Variable> ListName { get; init; }
    [IsNotNull]
    public partial data.@this<string> Key { get; init; }

    public Task<data.@this<type.list>> Run()
    {
        var data = Context.Variable.Get(ListName.Value);
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

        return Task.FromResult(global::app.data.@this<type.list>.Ok(
            new type.list { count = result.Count, value = result }, app.type.@this.FromName("list")));
    }
}
