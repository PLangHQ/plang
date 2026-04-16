using App.Variables;
using App.modules.signing.providers;

namespace App.modules.signing;

/// <summary>
/// Signs data using the configured signing provider.
/// </summary>
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

    /// <summary>Optional TTL in milliseconds. If set, Expires = Created + ExpiresInMs.</summary>
    public partial Data.@this<int>? ExpiresInMs { get; init; }

    [Provider]
    public partial ISigningProvider Signer { get; }

    public async Task<Data.@this> Run() => await Signer.SignAsync(this);
}
