using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.signing;

/// <summary>
/// Signs data using the configured signing provider.
/// Delegates to SignedData.CreateAsync — SignedData owns the signing pipeline.
/// </summary>
[Action("sign", Cacheable = false)]
public partial class sign : IContext
{
    /// <summary>The data to sign.</summary>
    public partial object? Data { get; init; }

    /// <summary>Optional override signing provider name.</summary>
    public partial string? Provider { get; init; }

    /// <summary>Contracts for this signature. Default: ["C0"].</summary>
    public partial List<string>? Contracts { get; init; }

    /// <summary>Optional headers to include in the signature envelope.</summary>
    public partial Dictionary<string, object>? Headers { get; init; }

    /// <summary>Optional TTL in milliseconds. If set, Expires = Created + ExpiresInMs.</summary>
    public partial int? ExpiresInMs { get; init; }

    public async Task<Data> Run()
        => await SignedData.CreateAsync(this, Context.Engine);
}
