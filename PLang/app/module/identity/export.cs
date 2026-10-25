using app.module.identity.code;

namespace app.module.identity;

/// <summary>
/// Exports the private key of an identity: refused until critical actions, which ask more than a yes before a key
/// leaves the app.
/// PLang: export identity 'alice' private key, write to %privateKey%
/// </summary>
[Action("export")]
public partial class Export : IContext
{
    public partial data.@this<global::app.type.item.text.@this>? Name { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<Identity>> Start() => await Identity.ExportAsync(this);
}
