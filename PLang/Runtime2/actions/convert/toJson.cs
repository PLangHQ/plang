using System.Text.Json;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.convert;

[Action("tojson")]
public partial class ToJson : IContext
{
    public partial object? Value { get; init; }
    [Default(false)]
    public partial bool Indent { get; init; }

    public Task<Data> Run()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = Indent
        };

        var json = JsonSerializer.Serialize(Value, options);
        return Task.FromResult(Data.Ok(json, PLang.Runtime2.Engine.Memory.Type.FromMime("application/json")));
    }
}
