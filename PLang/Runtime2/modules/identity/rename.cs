using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity.providers;

namespace PLang.Runtime2.modules.identity;

/// <summary>
/// Renames an identity. Keys are preserved, old name is removed from DataSource.
/// If the renamed identity is the default, updates %MyIdentity%.
/// PLang: rename identity 'alice' to 'alice-prod'
/// </summary>
[Action("rename", Cacheable = false)]
public partial class Rename : IContext
{
    public partial string Name { get; init; }
    public partial string NewName { get; init; }

    [Provider]
    public partial IIdentityProvider Identity { get; }

    public async Task<Data> Run() => await Identity.RenameAsync(this);
}
