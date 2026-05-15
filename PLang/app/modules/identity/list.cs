using app.Variables;
using app.modules.identity.code;

namespace app.modules.identity;

/// <summary>
/// Lists all non-archived identities.
/// PLang: get identities, write to %identities%
/// </summary>
[System.ComponentModel.Description("List all active (non-archived) identities in the store")]
[Action("list")]
public partial class list : IContext
{
    [Code]
    public partial IIdentity Identity { get; }

    public async Task<Data.@this> Run() => await Identity.ListAsync(this);
}
