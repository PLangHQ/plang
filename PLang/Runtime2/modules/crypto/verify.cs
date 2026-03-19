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
            hashBytes = Convert.FromHexString(Hash);
        }
        catch (FormatException)
        {
            return Engine.Memory.Data.FromError(new ActionError("Hash string is not valid hexadecimal", "InvalidHash", 400));
        }

        var provider = crypto.Hash.ResolveProvider(Context);
        var (bytes, _) = crypto.Hash.SerializeData(Data);
        return provider.Verify(bytes, hashBytes, Algorithm);
    }
}
