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

    public async Task<Data> Run()
    {
        var provider = Context.Engine.Providers.Get<IIdentityProvider>();
        if (!provider.Success) return provider;
        return await provider.Value!.RenameAsync(this);
    }
}
