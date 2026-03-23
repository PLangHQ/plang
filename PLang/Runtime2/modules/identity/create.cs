using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity.providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Creates a new identity with a key pair from the registered IKeyProvider.
/// PLang: create identity 'alice', set as default
/// </summary>
[Action("create", Cacheable = false)]
public partial class Create : IContext
{
    [Default("default")]
    public partial string Name { get; init; }

    [Default(false)]
    public partial bool SetAsDefault { get; init; }

    /// <summary>Optional provider name override. Uses default IKeyProvider if not specified.</summary>
    public partial string? Provider { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IIdentityProvider>();
        if (!provider.Success) return provider;
        return await provider.Value!.CreateAsync(this);
    }
}
