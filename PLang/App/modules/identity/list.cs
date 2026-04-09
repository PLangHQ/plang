using App.Variables;
using App.modules.identity.providers;

namespace App.modules.identity;

/// <summary>
/// Lists all non-archived identities.
/// PLang: get identities, write to %identities%
/// </summary>
[Action("list")]
public partial class list : IContext
{
    [Provider]
    public partial IIdentityProvider Identity { get; }

    public async Task<Data.@this> Run() => await Identity.ListAsync(this);
}
