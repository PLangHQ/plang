using App.Variables;
using App.modules.identity.providers;

namespace App.modules.identity;

/// <summary>
/// Gets an identity by name, or the default identity.
/// Auto-creates a default if none exist.
/// PLang: get identity 'alice', write to %identity%
/// </summary>
[System.ComponentModel.Description("Retrieve an identity by name, or the current default identity if Name is omitted")]
[Action("get")]
public partial class Get : IContext
{
    public partial Data.@this<string>? Name { get; init; }

    [Provider]
    public partial IIdentityProvider Identity { get; }

    public async Task<Data.@this> Run() => await Identity.GetAsync(this);
}
