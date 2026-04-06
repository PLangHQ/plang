using App.Variables;
using App.modules.identity.providers;

namespace App.modules.identity;

/// <summary>
/// Creates a new identity with a key pair from the registered IKeyProvider.
/// PLang: create identity 'alice', set as default
/// </summary>
[Action("create", Cacheable = false)]
public partial class Create : IContext
{
    [Default("default")]
    public partial string Name { get; init; }

    [Default(false)]
    public partial bool SetAsDefault { get; init; }

    /// <summary>Optional provider name override. Uses default IKeyProvider if not specified.</summary>
    public partial string? Provider { get; init; }

    [Provider]
    public partial IIdentityProvider Identity { get; }

    public async Task<Data> Run() => await Identity.CreateAsync(this);
}
