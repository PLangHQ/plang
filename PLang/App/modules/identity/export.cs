using App.Engine.Variables;
using App.modules.identity.providers;

namespace App.modules.identity;

/// <summary>
/// Exports the private key of an identity.
/// Returns the raw private key string.
/// PLang: export identity 'alice' private key, write to %privateKey%
/// </summary>
[Action("export")]
public partial class Export : IContext
{
    public partial string? Name { get; init; }

    [Provider]
    public partial IIdentityProvider Identity { get; }

    public async Task<Data> Run() => await Identity.ExportAsync(this);
}
