using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Lists all non-archived identities.
/// PLang: get all identities, write to %identities%
/// </summary>
[Action("getAll")]
public partial class GetAll : IContext
{
    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IIdentityProvider>();
        if (!provider.Success) return provider;
        return await provider.Value!.GetAllAsync(this);
    }
}
