using app.Variables;
using app.modules.signing.code;

namespace app.modules.signing;

/// <summary>
/// Signs data using the configured signing provider.
/// </summary>
[ModuleDescription("Sign and verify data payloads using pluggable cryptographic providers")]
[System.ComponentModel.Description("Sign a data payload and return a signed envelope via the configured signing provider")]
[Action("sign", Cacheable = false)]
public partial class sign : IContext
{
    /// <summary>The data to sign.</summary>
    [IsInitiated]
    public partial Data.@this? Data { get; init; }

    /// <summary>Contracts for this signature. Default: ["C0"].</summary>
    public partial Data.@this<List<string>>? Contracts { get; init; }

    /// <summary>Optional headers to include in the signature envelope.</summary>
    public partial Data.@this<Dictionary<string, object>>? Headers { get; init; }

    /// <summary>Optional TTL. If set, signature.Expires = Created + this duration.
    /// Wire form is ISO 8601 (e.g. <c>"PT5M"</c>) via the global TimeSpan converter.</summary>
    public partial Data.@this<TimeSpan>? Expires { get; init; }

    [Code]
    public partial ISigning Signer { get; }

    public async Task<Data.@this> Run() => await Signer.SignAsync(this);
}
