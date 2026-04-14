using App.Variables;

namespace App.modules.list;

[Action("group")]
public partial class Group : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    [IsNotNull]
    public partial Data.@this<string> Key { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        if (data.Value is not System.Collections.IList rawList)
            return Task.FromResult(Error(
                new App.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        var groups = new Dictionary<string, List<object?>>();
        foreach (var item in rawList)
        {
            var keyValue = ExtractKey(item, Key.Value!) ?? "";
            if (!groups.TryGetValue(keyValue, out var group))
            {
                group = new List<object?>();
                groups[keyValue] = group;
            }
            group.Add(item);
        }

        var result = groups.Select(g => new Dictionary<string, object?>
        {
            ["key"] = g.Key,
            ["steps"] = g.Value
        }).ToList();

        return Task.FromResult(Data(result, App.Data.Type.FromName("list")));
    }

    private static string? ExtractKey(object? item, string key)
    {
        if (item is IDictionary<string, object?> dict && dict.TryGetValue(key, out var val))
            return val?.ToString();
        if (item is System.Text.Json.JsonElement je && je.TryGetProperty(key, out var prop))
            return prop.GetString();
        return null;
    }
}
