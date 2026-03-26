using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity.providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Switches which identity is the default. Only one default at a time.
/// PLang: set default identity to 'alice'
/// </summary>
[Action("setDefault", Cacheable = false)]
public partial class SetDefault : IContext
{
    public partial string Name { get; init; }

    [Provider]
    public partial IIdentityProvider Identity { get; }

    public async Task<Data> Run() => await Identity.SetDefaultAsync(this);
}
