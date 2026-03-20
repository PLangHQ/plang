using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Exports the private key of an identity.
/// Returns the raw private key string.
/// PLang: export identity 'alice' private key, write to %privateKey%
/// </summary>
[Action("export")]
public partial class Export : IContext
{
    public partial string? Name { get; init; }

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IIdentityProvider>();
        if (provider == null)
            return Data.FromError(new ActionError("No identity provider registered", "NoProvider", 500));
        return await provider.ExportAsync(this);
    }
}
