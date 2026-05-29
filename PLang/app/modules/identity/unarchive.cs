using app.variable;
using app.modules.identity.code;

namespace app.modules.identity;

/// <summary>
/// Restores an archived identity.
/// Idempotent — unarchiving an active identity succeeds.
/// PLang: unarchive identity 'alice'
/// </summary>
[Action("unarchive", Cacheable = false)]
public partial class Unarchive : IContext
{
    public partial data.@this<string> Name { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<Identity>> Run() => await Identity.UnarchiveAsync(this);
}
