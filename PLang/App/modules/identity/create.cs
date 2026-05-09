using App.Variables;
using App.modules.identity.code;

namespace App.modules.identity;

/// <summary>
/// Creates a new identity with a key pair from the registered IKey.
/// PLang: create identity 'alice', set as default
/// </summary>
[System.ComponentModel.Description("Create a new named identity with a generated key pair, optionally setting it as default")]
[Action("create", Cacheable = false)]
public partial class Create : IContext
{
    [Default("default")]
    public partial Data.@this<string> Name { get; init; }

    [Default(false)]
    public partial Data.@this<bool> SetAsDefault { get; init; }

    /// <summary>Optional provider name override. Uses default IKey if not specified.</summary>
    public partial Data.@this<string>? Provider { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<Data.@this> Run() => await Identity.CreateAsync(this);
}
