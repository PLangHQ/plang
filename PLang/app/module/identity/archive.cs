using app.variable;
using app.module.identity.code;

namespace app.module.identity;

/// <summary>
/// Archives an identity (soft-delete).
/// Idempotent — archiving an already-archived identity succeeds.
/// PLang: archive identity 'alice'
/// PLang: archive identity 'alice', force
/// </summary>
[Action("archive", Cacheable = false)]
public partial class Archive : IContext
{
    public partial data.@this<global::app.type.text.@this> Name { get; init; }

    /// <summary>When true, allows archiving even if it's the default identity.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> Force { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<Identity>> Run() => await Identity.ArchiveAsync(this);
}
