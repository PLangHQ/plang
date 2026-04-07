using System.Text.Json;

namespace App.Data.Navigators;

/// <summary>
/// Navigates string values that contain JSON objects or arrays.
/// Parses on access, then delegates to the appropriate navigator.
/// </summary>
public sealed class JsonStringNavigator : INavigator
{
    private const int MaxJsonStringSize = 10 * 1024 * 1024; // 10MB

    public bool CanNavigate(Data.@this data)
    {
        if (data.Value is not string str)
            return false;

        if (str.Length > MaxJsonStringSize)
            return false;

        var trimmed = str.TrimStart();
        return trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '[');
    }

    public Data.@this Navigate(Data.@this data, string key)
    {
        if (data.Value is not string str)
            return Data.@this.Null(key);

        try
        {
            var doc = JsonDocument.Parse(str);
            var parsed = UnwrapElement(doc.RootElement);
            if (parsed == null) return Data.@this.Null(key);

            var parsedData = new Data.@this(data.Name, parsed, parent: data);
            return ValueNavigators.Navigate(parsedData, key);
        }
        catch (JsonException)
        {
            return Data.@this.Null(key);
        }
    }

    private static object? UnwrapElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => UnwrapObject(element),
            JsonValueKind.Array => UnwrapArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static Dictionary<string, object?> UnwrapObject(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = UnwrapElement(prop.Value);
        }
        return dict;
    }

    private static List<object?> UnwrapArray(JsonElement element)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(UnwrapElement(item));
        }
        return list;
    }
}
