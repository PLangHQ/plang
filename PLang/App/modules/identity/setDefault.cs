using App.Variables;
using App.modules.identity.code;

namespace App.modules.identity;

/// <summary>
/// Switches which identity is the default. Only one default at a time.
/// PLang: set default identity to 'alice'
/// </summary>
[System.ComponentModel.Description("Switch the default identity to the named one; only one identity can be default at a time")]
[Action("setDefault", Cacheable = false)]
public partial class SetDefault : IContext
{
    public partial Data.@this<string> Name { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<Data.@this> Run() => await Identity.SetDefaultAsync(this);
}
