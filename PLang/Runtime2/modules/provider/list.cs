using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.provider;

/// <summary>
/// Lists registered providers, optionally filtered by type.
/// PLang: list signing providers
/// </summary>
[Action("list", Cacheable = false)]
public partial class list : IContext
{
    public partial string? Type { get; init; }

    public async Task<Data> Run()
    {
        if (string.IsNullOrEmpty(Type))
            return Data.Ok(Context.Engine.Providers.List());

        var providerType = Context.Engine.Providers.ResolveType(Type);
        if (providerType == null)
            return Data.FromError(new Engine.Errors.ActionError($"Unknown provider type '{Type}'", "UnknownType", 400));

        return Context.Engine.Providers.List(providerType);
    }
}
