using System.Text.Json;
using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.convert;

[Action("fromjson")]
public partial class FromJson : IContext
{
    public partial string Value { get; init; }

    public Task<Data> Run()
    {
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(Value);
            var result = UnwrapJsonElement(element);
            return Task.FromResult(Data.Ok(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                new Errors.ValidationError($"Invalid JSON: {ex.Message}", "JsonParseError")));
        }
    }

    private static object? UnwrapJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => UnwrapJsonElement(p.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(UnwrapJsonElement).ToList() as object,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
