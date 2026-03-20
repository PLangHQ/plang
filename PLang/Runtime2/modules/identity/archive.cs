using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Archives an identity (soft-delete). Cannot archive the default identity.
/// Idempotent — archiving an already-archived identity succeeds.
/// PLang: archive identity 'alice'
/// </summary>
[Action("archive", Cacheable = false)]
public partial class Archive : IContext
{
    public partial string Name { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IIdentityProvider>();
        if (!provider.Success) return provider;
        return await provider.Value!.ArchiveAsync(this);
    }
}
