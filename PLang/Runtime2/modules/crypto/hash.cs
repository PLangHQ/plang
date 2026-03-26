using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.crypto.providers;

namespace PLang.Runtime2.modules.crypto;

[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    public partial Data? Data { get; init; }

    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    [Provider]
    public partial ICryptoProvider Crypto { get; }

    public async Task<Engine.Memory.Data> Run()
    {
        if (Data?.Value == null)
            return Engine.Memory.Data.FromError(new ActionError("Data cannot be null", "ValidationError", 400));

        return Crypto.Hash(this);
    }
}
