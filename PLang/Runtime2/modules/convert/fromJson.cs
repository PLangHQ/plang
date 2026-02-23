using System.Text.Json;
using PLang.Runtime2.Engine.Memory;

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
            var result = Data.UnwrapJsonElement(element);
            return Task.FromResult(Data.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError(ex.Message, "JsonDepthExceeded")));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Invalid JSON: {ex.Message}", "JsonParseError")));
        }
    }
}
