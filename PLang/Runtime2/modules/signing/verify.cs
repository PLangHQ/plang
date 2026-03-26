using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.signing.providers;

namespace PLang.Runtime2.modules.signing;

/// <summary>
/// Verifies a signed data envelope.
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

    [Provider]
    public partial ISigningProvider Signer { get; }

    public async Task<Data> Run() => await Signer.VerifyAsync(this);
}
