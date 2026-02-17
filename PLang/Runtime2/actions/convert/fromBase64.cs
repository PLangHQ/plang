using System.Text;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.convert;

[Action("frombase64")]
public partial class FromBase64 : IContext
{
    public partial string Value { get; init; }
    [Default(false)]
    public partial bool AsBytes { get; init; }

    public Task<Data> Run()
    {
        try
        {
            var bytes = Convert.FromBase64String(Value);

            if (AsBytes)
                return Task.FromResult(Data.Ok(bytes, PLang.Runtime2.Engine.Memory.Type.FromName("bytes")));

            var result = Encoding.UTF8.GetString(bytes);
            return Task.FromResult(Data.Ok(result, PLang.Runtime2.Engine.Memory.Type.String));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Invalid base64: {ex.Message}", "ConversionError")));
        }
    }
}
