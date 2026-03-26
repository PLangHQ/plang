using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.signing.providers;

namespace PLang.Runtime2.modules.signing;

/// <summary>
/// Signs data using the configured signing provider.
/// </summary>
[Action("sign", Cacheable = false)]
public partial class sign : IContext
{
    /// <summary>The data to sign.</summary>
    public partial object? Data { get; init; }

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
