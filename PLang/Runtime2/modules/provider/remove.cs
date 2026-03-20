using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;

namespace PLang.Runtime2.modules.provider;

/// <summary>
/// Removes a named provider from the registry.
/// PLang: remove provider 'custom-crypto' from signing
/// </summary>
[Action("remove", Cacheable = false)]
public partial class remove : IContext
{
    public partial string Name { get; init; }
    public partial string? Type { get; init; }

    public async Task<Data> Run()
    {
        if (string.IsNullOrEmpty(Name))
            return Data.FromError(new ActionError("Provider name is required", "ValidationError", 400));

        // Default to ISigningProvider if no type specified
        var providerType = ResolveProviderType(Type);
        if (providerType == null)
            return Data.FromError(new ActionError($"Unknown provider type '{Type}'", "UnknownType", 400));

        var removeMethod = typeof(EngineProviders).GetMethod("Remove")!.MakeGenericMethod(providerType);
        return (Data)removeMethod.Invoke(Context.Engine.Providers, new object[] { Name })!;
    }

    internal static System.Type? ResolveProviderType(string? typeName)
    {
        return typeName?.ToLowerInvariant() switch
        {
            "signing" or "isigningprovider" => typeof(ISigningProvider),
            "key" or "ikeyprovider" => typeof(IKeyProvider),
            "crypto" or "icryptoprovider" => typeof(PLang.Runtime2.modules.crypto.providers.ICryptoProvider),
            null or "" => typeof(ISigningProvider),
            _ => null
        };
    }
}
