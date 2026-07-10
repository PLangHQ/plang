using app.variable;
using app.module.identity.code;

namespace app.module.identity;

/// <summary>
/// Lists all non-archived identities.
/// PLang: get identities, write to %identities%
/// </summary>
[Action("list")]
public partial class list : IContext
{
    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<global::app.type.item.list.@this<Identity>>> Run() => await Identity.ListAsync(this);
}
