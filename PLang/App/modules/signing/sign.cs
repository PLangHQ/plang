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
    public partial Data? Data { get; init; }

    /// <summary>Contracts for this signature. Default: ["C0"].</summary>
    public partial List<string>? Contracts { get; init; }

    /// <summary>Optional headers to include in the signature envelope.</summary>
    public partial Dictionary<string, object>? Headers { get; init; }

    /// <summary>Optional TTL in milliseconds. If set, Expires = Created + ExpiresInMs.</summary>
    public partial int? ExpiresInMs { get; init; }

    [Provider]
    public partial ISigningProvider Signer { get; }

    public async Task<Data> Run() => await Signer.SignAsync(this);
}
