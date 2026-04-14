using App.Variables;

namespace App.modules.list;

/// <summary>
/// Checks if any item in a list matches a condition on a property.
/// Usage: any %list% where "level" != "high", write to %hasNonHigh%
/// </summary>
[Action("any")]
public partial class Any : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    [IsNotNull]
    public partial Data.@this<string> Key { get; init; }
    [IsNotNull]
    public partial Data.@this<condition.Operator> Operator { get; init; }
    public partial Data.@this Value { get; init; }

    public Task<Data.@this> Run()
    {
        var data = Context.Variables.Get(ListName);
        if (data.Value is not System.Collections.IList rawList)
            return Task.FromResult(Error(
                new App.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        foreach (var item in rawList)
        {
            var propValue = ExtractProperty(item, Key.Value!);
            var left = propValue != null ? new Data.@this("", propValue) : null;
            var right = Value.Value != null ? new Data.@this("", Value.Value) : null;

            if (Operator.Value!.Evaluate(left, right))
                return Task.FromResult(Data(true, App.Data.Type.FromName("bool")));
        }

        return Task.FromResult(Data(false, App.Data.Type.FromName("bool")));
    }

    private static object? ExtractProperty(object? item, string key)
    {
        if (item is IDictionary<string, object?> dict && dict.TryGetValue(key, out var val))
            return val;
        if (item is System.Text.Json.JsonElement je && je.TryGetProperty(key, out var prop))
            return prop.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => prop.GetString(),
                System.Text.Json.JsonValueKind.Number => prop.GetInt64(),
                System.Text.Json.JsonValueKind.True => true,
                System.Text.Json.JsonValueKind.False => false,
                _ => prop.ToString()
            };
        return null;
    }
}
