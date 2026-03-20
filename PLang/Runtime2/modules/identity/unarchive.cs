using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Restores an archived identity.
/// Idempotent — unarchiving an active identity succeeds.
/// PLang: unarchive identity 'alice'
/// </summary>
[Action("unarchive", Cacheable = false)]
public partial class Unarchive : IContext
{
    public partial string Name { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IIdentityProvider>();
        if (!provider.Success) return provider;
        return await provider.Value!.UnarchiveAsync(this);
    }
}
