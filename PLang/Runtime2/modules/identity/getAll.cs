using PLang.Runtime2.Engine.Errors;
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
        if (provider == null)
            return Data.FromError(new ActionError("No identity provider registered", "NoProvider", 500));
        return await provider.GetAllAsync(this);
    }
}
