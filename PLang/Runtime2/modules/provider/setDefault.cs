using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.provider;

/// <summary>
/// Sets a named provider as the default for its type.
/// PLang: set default signing provider to 'custom'
/// </summary>
[Action("setDefault", Cacheable = false)]
public partial class setDefault : IContext
{
    public partial string Name { get; init; }
    public partial string? Type { get; init; }

    public async Task<Data> Run()
    {
        var providerType = Context.Engine.Providers.ResolveType(Type);
        if (providerType == null)
            return Data.FromError(new Engine.Errors.ActionError($"Unknown provider type '{Type}'", "UnknownType", 400));

        return Context.Engine.Providers.SetDefault(providerType, Name);
    }
}
