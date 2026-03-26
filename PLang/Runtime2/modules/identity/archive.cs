using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity.providers;

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

    [Provider]
    public partial IIdentityProvider Identity { get; }

    public async Task<Data> Run() => await Identity.ArchiveAsync(this);
}
