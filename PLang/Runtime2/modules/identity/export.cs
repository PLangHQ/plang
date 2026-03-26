using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity.providers;

namespace PLang.Runtime2.modules.identity;

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
