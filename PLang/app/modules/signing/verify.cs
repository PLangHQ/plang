using app.Variables;
using app.modules.signing.code;

namespace app.modules.signing;

/// <summary>
/// Verifies a signed data envelope.
/// </summary>
[System.ComponentModel.Description("Verify a signed envelope against expected contracts and headers")]
[Action("verify", Cacheable = false)]
public partial class verify : IContext
{
    /// <summary>The signed data to verify.</summary>
    public partial data.@this? Data { get; init; }

    /// <summary>Required contracts for verification.</summary>
    public partial data.@this<List<string>>? Contracts { get; init; }

    /// <summary>Expected headers to match against signed headers.</summary>
    public partial data.@this<Dictionary<string, object>>? Headers { get; init; }

    /// <summary>Optional timeout override in milliseconds.</summary>
    public partial data.@this<long>? TimeoutMs { get; init; }

    [Code]
    public partial ISigning Signer { get; }

    public async Task<data.@this> Run() => await Signer.VerifyAsync(this);
}
