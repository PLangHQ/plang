using App.Variables;
using App.modules.identity.code;

namespace App.modules.identity;

/// <summary>
/// Exports the private key of an identity.
/// Returns the raw private key string.
/// PLang: export identity 'alice' private key, write to %privateKey%
/// </summary>
[System.ComponentModel.Description("Export the private key of an identity as a raw string")]
[Action("export")]
public partial class Export : IContext
{
    public partial Data.@this<string>? Name { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<Data.@this> Run() => await Identity.ExportAsync(this);
}
