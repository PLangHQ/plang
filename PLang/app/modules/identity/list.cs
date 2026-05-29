using app.variable;
using app.modules.identity.code;

namespace app.modules.identity;

/// <summary>
/// Lists all non-archived identities.
/// PLang: get identities, write to %identities%
/// </summary>
[Action("list")]
public partial class list : IContext
{
    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<List<Identity>>> Run() => await Identity.ListAsync(this);
}
