using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Gets an identity by name, or the default identity.
/// Auto-creates a default if none exist.
/// PLang: get identity 'alice', write to %identity%
/// </summary>
[Action("get")]
public partial class Get : IContext
{
    public partial string? Name { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IIdentityProvider>();
        if (provider == null)
            return Data.FromError(new ActionError("No identity provider registered", "NoProvider", 500));
        return await provider.GetAsync(this);
    }
}
