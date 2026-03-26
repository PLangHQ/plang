using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Runtime2.modules.crypto;

[Action("verify", Cacheable = false)]
public partial class Verify : IContext
{
    public partial Data? Data { get; init; }
    public partial string Hash { get; init; }

    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    [Provider]
    public partial ICryptoProvider Crypto { get; }

    public async Task<Engine.Memory.Data> Run()
    {
        if (Data?.Value == null)
            return Engine.Memory.Data.FromError(new ActionError("Data cannot be null", "ValidationError", 400));

        if (string.IsNullOrEmpty(Hash))
            return Engine.Memory.Data.FromError(new ActionError("Hash cannot be null or empty", "InvalidHash", 400));

        return Crypto.Verify(this);
    }
}
