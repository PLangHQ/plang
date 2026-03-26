using System.Text;
using System.Text.Json;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Runtime2.modules.crypto;

[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    public partial object? Data { get; init; }
    public partial string Hash { get; init; }

    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    public async Task<Data> Run()
    {
        if (Data == null)
            return Engine.Memory.Data.FromError(new ActionError("Data cannot be null", "ValidationError", 400));

        if (string.IsNullOrEmpty(Hash))
            return Engine.Memory.Data.FromError(new ActionError("Hash cannot be null or empty", "InvalidHash", 400));

        byte[] hashBytes;
        try
        {
            hashBytes = Convert.FromBase64String(Hash);
        }
        catch (FormatException)
        {
            return Engine.Memory.Data.FromError(new ActionError("Hash string is not valid base64", "InvalidHash", 400));
        }

        var providerResult = Context.Engine.Providers.Get<ICryptoProvider>();
        if (!providerResult.Success) return providerResult;

        var bytes = Data is byte[] raw ? raw : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Data));
        return providerResult.Value!.Verify(bytes, hashBytes, Algorithm);
    }
}
