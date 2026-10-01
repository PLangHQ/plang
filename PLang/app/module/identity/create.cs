using app.module.identity.code;

namespace app.module.identity;

/// <summary>
/// Creates a new identity with a key pair from the registered IKey.
/// PLang: create identity 'alice', set as default
/// </summary>
[Action("create", Cacheable = false)]
public partial class Create : IContext
{
    [Default("default")]
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    /// <summary>Whether the new identity becomes the default one.</summary>
    [Default(false)]
    public partial data.@this<global::app.type.item.@bool.@this> Default { get; init; }

    /// <summary>Optional provider name override. Uses default IKey if not specified.</summary>
    public partial data.@this<global::app.type.item.text.@this>? Provider { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<Identity>> Start() => await Identity.CreateAsync(this);
}
