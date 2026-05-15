using System.Text.Json;

namespace app.variables.navigators;

/// <summary>
/// Navigates string values that contain JSON objects or arrays.
/// Parses on access, then delegates to the appropriate navigator.
/// </summary>
public sealed class JsonString : INavigator
{
    private const int MaxJsonStringSize = 10 * 1024 * 1024; // 10MB

    /// <summary>Maximum number of JSON elements (objects, arrays, values) allowed during unwrap. Prevents object amplification attacks.</summary>
    private const int MaxElementCount = 100_000;

    /// <summary>Maximum nesting depth for JSON unwrap. Defense-in-depth alongside JsonDocument's own depth limit.</summary>
    private const int MaxDepth = 64;

    public bool CanNavigate(global::app.data.@this data)
    {
        if (data.Value is not string str)
            return false;

        if (str.Length > MaxJsonStringSize)
            return false;

        var trimmed = str.TrimStart();
        return trimmed.Length > 0 && (trimmed[0] == '{' || trimmed[0] == '[');
    }

    public global::app.data.@this Navigate(global::app.data.@this data, string key)
    {
        if (data.Value is not string str)
            return global::app.data.@this.NotFound(key);

        try
        {
            var doc = JsonDocument.Parse(str);
            int elementCount = 0;
            var parsed = UnwrapElement(doc.RootElement, 0, ref elementCount);
            if (parsed == null) return global::app.data.@this.NotFound(key);

            var parsedData = new data.@this(data.Name, parsed, parent: data);
            return ValueNavigators.Navigate(parsedData, key);
        }
        catch (JsonException)
        {
            return global::app.data.@this.NotFound(key);
        }
    }

    private static object? UnwrapElement(JsonElement element, int depth, ref int elementCount)
    {
        if (depth > MaxDepth)
            throw new JsonException($"JSON nesting exceeds maximum depth of {MaxDepth}");

        elementCount++;
        if (elementCount > MaxElementCount)
            throw new JsonException($"JSON element count exceeds maximum of {MaxElementCount:N0}");

        return element.ValueKind switch
        {
            JsonValueKind.Object => UnwrapObject(element, depth, ref elementCount),
            JsonValueKind.Array => UnwrapArray(element, depth, ref elementCount),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static Dictionary<string, object?> UnwrapObject(JsonElement element, int depth, ref int elementCount)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = UnwrapElement(prop.Value, depth + 1, ref elementCount);
        }
        return dict;
    }

    private static List<object?> UnwrapArray(JsonElement element, int depth, ref int elementCount)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(UnwrapElement(item, depth + 1, ref elementCount));
        }
        return list;
    }
}
