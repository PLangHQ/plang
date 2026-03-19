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

        var provider = ResolveProvider(Context);
        var (bytes, format) = SerializeData(Data);
        var hashResult = provider.Hash(bytes, Algorithm);
        if (!hashResult.Success)
            return hashResult;

        var hex = FormatHash((byte[])hashResult.Value!);

        return Engine.Memory.Data.Ok(new HashedData
        {
            Algorithm = Algorithm.ToLowerInvariant(),
            Format = format,
            Hash = hex
        });
    }

    internal static (byte[] bytes, string format) SerializeData(object data)
    {
        if (data is byte[] raw)
            return (raw, "raw");

        var json = JsonSerializer.Serialize(data);
        return (Encoding.UTF8.GetBytes(json), "json");
    }

    internal static string FormatHash(byte[] hashBytes)
    {
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    internal static ICryptoProvider ResolveProvider(PLang.Runtime2.Engine.Context.PLangContext context)
    {
        return context.Engine.Providers.GetOrDefault<ICryptoProvider>(new DefaultProvider());
    }
}
