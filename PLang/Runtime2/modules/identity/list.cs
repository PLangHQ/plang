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
    [Provider]
    public partial IIdentityProvider Identity { get; }

    public async Task<Data> Run() => await Identity.ListAsync(this);
}
