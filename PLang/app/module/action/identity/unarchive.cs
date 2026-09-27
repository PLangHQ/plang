using app.module.action.identity.code;

namespace app.module.action.identity;

/// <summary>
/// Restores an archived identity.
/// Idempotent — unarchiving an active identity succeeds.
/// PLang: unarchive identity 'alice'
/// </summary>
[Action("unarchive", Cacheable = false)]
public partial class Unarchive : IContext
{
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<Identity>> Start() => await Identity.UnarchiveAsync(this);
}
