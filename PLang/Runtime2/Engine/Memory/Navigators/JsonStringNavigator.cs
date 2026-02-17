using System.Text.Json;

namespace PLang.Runtime2.Engine.Memory.Navigators;

/// <summary>
/// Navigates string values that contain JSON objects or arrays.
/// Parses on access, then delegates to the appropriate navigator.
/// </summary>
public class JsonStringNavigator : IValueNavigator
{
    public bool CanNavigate(object value)
    {
        if (value is not string str)
            return false;

        var trimmed = str.TrimStart();
        return trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '[');
    }

    public object? GetProperty(object value, string key)
    {
        if (value is not string str)
            return null;

        try
        {
            var doc = JsonDocument.Parse(str);
            var parsed = UnwrapElement(doc.RootElement);
            if (parsed == null) return null;

            return ValueNavigators.Navigate(parsed, key);
        }
        catch (JsonException)
        {
            return null;
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
