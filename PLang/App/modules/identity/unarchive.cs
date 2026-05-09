using App.Variables;
using App.modules.identity.code;

namespace App.modules.identity;

/// <summary>
/// Restores an archived identity.
/// Idempotent — unarchiving an active identity succeeds.
/// PLang: unarchive identity 'alice'
/// </summary>
[System.ComponentModel.Description("Restore a previously archived identity, making it active and visible again")]
[Action("unarchive", Cacheable = false)]
public partial class Unarchive : IContext
{
    public partial Data.@this<string> Name { get; init; }

    [Provider]
    public partial IIdentity Identity { get; }

    public async Task<Data.@this> Run() => await Identity.UnarchiveAsync(this);
}
