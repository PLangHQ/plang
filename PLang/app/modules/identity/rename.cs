using app.Variables;
using app.modules.identity.code;

namespace app.modules.identity;

/// <summary>
/// Renames an identity. Keys are preserved, old name is removed from DataSource.
/// If the renamed identity is the default, updates %MyIdentity%.
/// PLang: rename identity 'alice' to 'alice-prod'
/// </summary>
[System.ComponentModel.Description("Rename an identity from Name to NewName, preserving its key pair")]
[Action("rename", Cacheable = false)]
public partial class Rename : IContext
{
    public partial data.@this<string> Name { get; init; }
    public partial data.@this<string> NewName { get; init; }

    [Code]
    public partial IIdentity Identity { get; }

    public async Task<data.@this> Run() => await Identity.RenameAsync(this);
}
