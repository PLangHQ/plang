using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

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
        {
            // List all providers across all types
            return Data.Ok(Context.Engine.Providers.List());
        }

        var providerType = remove.ResolveProviderType(Type);
        if (providerType == null)
            return Data.FromError(new ActionError($"Unknown provider type '{Type}'", "UnknownType", 400));

        var listMethod = typeof(EngineProviders).GetMethod("List", System.Type.EmptyTypes)!;
        var genericList = listMethod.MakeGenericMethod(providerType);
        var result = genericList.Invoke(Context.Engine.Providers, null);
        return Data.Ok(result);
    }
}
