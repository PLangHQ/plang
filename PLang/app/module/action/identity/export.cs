using app.variable;
using app.module.action.identity.code;

namespace app.module.action.identity;

/// <summary>
/// Exports the private key of an identity.
/// Returns the raw private key string.
/// PLang: export identity 'alice' private key, write to %privateKey%
/// </summary>
[Action("export")]
public partial class Export : IContext
{
    public partial data.@this<global::app.type.item.text.@this>? Name { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<Identity>> Run() => await Identity.ExportAsync(this);
}
