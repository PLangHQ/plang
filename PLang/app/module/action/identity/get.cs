using app.variable;
using app.module.action.identity.code;

namespace app.module.action.identity;

/// <summary>
/// Gets an identity by name, or the default identity.
/// Auto-creates a default if none exist.
/// PLang: get identity 'alice', write to %identity%
/// </summary>
[Action("get")]
public partial class Get : IContext
{
    public partial data.@this<global::app.type.item.text.@this>? Name { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<Identity>> Run() => await Identity.GetAsync(this);
}
