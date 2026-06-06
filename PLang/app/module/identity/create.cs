using app.variable;
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
    public partial data.@this<global::app.type.text.@this> Name { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> SetAsDefault { get; init; }

    /// <summary>Optional provider name override. Uses default IKey if not specified.</summary>
    public partial data.@this<global::app.type.text.@this>? Provider { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<Identity>> Run() => await Identity.CreateAsync(this);
}
