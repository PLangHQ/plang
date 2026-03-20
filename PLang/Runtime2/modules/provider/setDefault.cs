using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

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
        if (string.IsNullOrEmpty(Name))
            return Data.FromError(new ActionError("Provider name is required", "ValidationError", 400));

        var providerType = remove.ResolveProviderType(Type);
        if (providerType == null)
            return Data.FromError(new ActionError($"Unknown provider type '{Type}'", "UnknownType", 400));

        var setDefaultMethod = typeof(EngineProviders).GetMethod("SetDefault")!.MakeGenericMethod(providerType);
        return (Data)setDefaultMethod.Invoke(Context.Engine.Providers, new object[] { Name })!;
    }
}
