using app.variable;
using app.module.signing.code;

namespace app.module.signing;

/// <summary>
/// Verifies a signed Data.
/// </summary>
[Action("verify", Cacheable = false)]
public partial class verify : IContext
{
    /// <summary>The signed data to verify.</summary>
    public partial data.@this? Data { get; init; }

    /// <summary>Required contracts for verification.</summary>
    public partial data.@this<global::app.type.list.@this>? Contracts { get; init; }

    /// <summary>Expected headers to match against signed headers.</summary>
    public partial data.@this<global::app.type.dict.@this>? Headers { get; init; }

    /// <summary>Freshness timeout in milliseconds. Default: 300000 (5 min).</summary>
    [Default(300_000)]
    public partial data.@this<global::app.type.number.@this> TimeoutMs { get; init; }

    /// <summary>
    /// When true, skip the Created-age wire-freshness check (step 2) and the
    /// nonce-replay check (step 4). The signature's own <c>Expires</c> field
    /// becomes the only time bound (null = permanent, set = enforced).
    /// Use for verifying long-lived stored artifacts like permission grants,
    /// where the same nonce naturally re-presents across reads and the
    /// signature is intended to outlive the wire-freshness window.
    /// </summary>
    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> SkipFreshnessCheck { get; init; }

    [Code]
    public partial ISigning Signer { get; }

    public async Task<data.@this<global::app.type.@bool.@this>> Run() => await Signer.VerifyAsync(this);
}
