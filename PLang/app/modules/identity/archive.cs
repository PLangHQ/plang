using app.Variables;
using app.modules.identity.code;

namespace app.modules.identity;

/// <summary>
/// Archives an identity (soft-delete).
/// Idempotent — archiving an already-archived identity succeeds.
/// PLang: archive identity 'alice'
/// PLang: archive identity 'alice', force
/// </summary>
[ModuleDescription("Manage cryptographic identities: create, list, set default, rename, archive, and export keys")]
[System.ComponentModel.Description("Soft-delete an identity by archiving it; hidden from listings but keys are preserved")]
[Action("archive", Cacheable = false)]
public partial class Archive : IContext
{
    public partial data.@this<string> Name { get; init; }

    /// <summary>When true, allows archiving even if it's the default identity.</summary>
    [Default(false)]
    public partial data.@this<bool> Force { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this> Run() => await Identity.ArchiveAsync(this);
}
