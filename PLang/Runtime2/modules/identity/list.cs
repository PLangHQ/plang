using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity.providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Lists all non-archived identities.
/// PLang: get identities, write to %identities%
/// </summary>
[Action("list")]
public partial class list : IContext
{
    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IIdentityProvider>();
        if (!provider.Success) return provider;
        return await provider.Value!.ListAsync(this);
    }
}
