using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Switches which identity is the default. Only one default at a time.
/// PLang: set default identity to 'alice'
/// </summary>
[Action("setDefault", Cacheable = false)]
public partial class SetDefault : IContext
{
    public partial string Name { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IIdentityProvider>();
        if (provider == null)
            return Data.FromError(new ActionError("No identity provider registered", "NoProvider", 500));
        return await provider.SetDefaultAsync(this);
    }
}
