using app.variable;
using app.module.identity.code;

namespace app.module.identity;

/// <summary>
/// Renames an identity. Keys are preserved, old name is removed from DataSource.
/// If the renamed identity is the default, updates %MyIdentity%.
/// PLang: rename identity 'alice' to 'alice-prod'
/// </summary>
[Action("rename", Cacheable = false)]
public partial class Rename : IContext
{
    public partial data.@this<global::app.type.text.@this> Name { get; init; }
    public partial data.@this<global::app.type.text.@this> NewName { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this<Identity>> Run() => await Identity.RenameAsync(this);
}
