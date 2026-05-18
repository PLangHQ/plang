using app.variables;
using app.modules.identity.code;

namespace app.modules.identity;

/// <summary>
/// Creates a new identity with a key pair from the registered IKey.
/// PLang: create identity 'alice', set as default
/// </summary>
[System.ComponentModel.Description("Create a new named identity with a generated key pair, optionally setting it as default")]
[Action("create", Cacheable = false)]
public partial class Create : IContext
{
    [Default("default")]
    public partial data.@this<string> Name { get; init; }

    [Default(false)]
    public partial data.@this<bool> SetAsDefault { get; init; }

    /// <summary>Optional provider name override. Uses default IKey if not specified.</summary>
    public partial data.@this<string>? Provider { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this> Run() => await Identity.CreateAsync(this);
}
