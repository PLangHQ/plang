using app.variable;
using app.module.identity.code;

namespace app.module.identity;

/// <summary>
/// Switches which identity is the default. Only one default at a time.
/// PLang: set default identity to 'alice'
/// </summary>
[Action("setDefault", Cacheable = false)]
public partial class SetDefault : IContext
{
    public partial data.@this<global::app.type.text.@this> Name { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<Identity>> Run() => await Identity.SetDefaultAsync(this);
}
