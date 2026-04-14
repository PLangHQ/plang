using App.Variables;
using App.modules.identity.providers;

namespace App.modules.identity;

/// <summary>
/// Archives an identity (soft-delete).
/// Idempotent — archiving an already-archived identity succeeds.
/// PLang: archive identity 'alice'
/// PLang: archive identity 'alice', force
/// </summary>
[Action("archive", Cacheable = false)]
public partial class Archive : IContext
{
    public partial Data.@this<string> Name { get; init; }

    /// <summary>When true, allows archiving even if it's the default identity.</summary>
    [Default(false)]
    public partial Data.@this<bool> Force { get; init; }

    [Provider]
    public partial IIdentityProvider Identity { get; }

    public async Task<Data.@this> Run() => await Identity.ArchiveAsync(this);
}
