using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity.providers;

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

    [Provider]
    public partial IIdentityProvider Identity { get; }

    public async Task<Data> Run() => await Identity.UnarchiveAsync(this);
}
