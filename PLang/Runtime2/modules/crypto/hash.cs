using System.Text;
using System.Text.Json;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Runtime2.modules.crypto;

[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    public partial object? Data { get; init; }

    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    public async Task<Data> Run()
    {
        if (Data == null)
            return Engine.Memory.Data.FromError(new ActionError("Data cannot be null", "ValidationError", 400));

        var providerResult = Context.Engine.Providers.Get<ICryptoProvider>();
        if (!providerResult.Success) return providerResult;

        var bytes = Data is byte[] raw ? raw : Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Data));
        var hashResult = providerResult.Value!.Hash(bytes, Algorithm);
        if (!hashResult.Success)
            return hashResult;

        return Engine.Memory.Data.Ok(Convert.ToBase64String((byte[])hashResult.Value!), Engine.Memory.Type.FromName(Algorithm.ToLowerInvariant()));
    }
}
