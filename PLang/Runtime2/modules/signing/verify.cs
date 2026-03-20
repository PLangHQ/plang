using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.signing;

/// <summary>
/// Verifies a signed data envelope.
/// Delegates to SignedData.VerifyAsync — SignedData owns its own verification.
/// </summary>
[Action("verify", Cacheable = false)]
public partial class verify : IContext
{
    /// <summary>The signed data to verify.</summary>
    public partial Data? Data { get; init; }

    /// <summary>Required contracts for verification.</summary>
    public partial List<string>? Contracts { get; init; }

    /// <summary>Expected headers to match against signed headers.</summary>
    public partial Dictionary<string, object>? Headers { get; init; }

    /// <summary>Optional timeout override in milliseconds.</summary>
    public partial long? TimeoutMs { get; init; }

    public async Task<Data> Run()
    {
        if (Data?.Signature == null)
            return Engine.Memory.Data.FromError(new ActionError("Data has no signature", "NoSignature", 400));

        return await Data.Signature.VerifyAsync(Contracts, Headers, TimeoutMs, Data.Value);
    }
}
