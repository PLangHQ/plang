using App.Variables;

namespace App.modules.provider;

/// <summary>
/// Removes a named provider from the registry.
/// PLang: remove provider 'custom-crypto' from signing
/// </summary>
[Action("remove", Cacheable = false)]
public partial class remove : IContext
{
    /// <summary>Name of the provider to remove.</summary>
    public partial string Name { get; init; }

    /// <summary>Provider type name (e.g., "signing", "crypto", "identity", "key").</summary>
    public partial string? Type { get; init; }

    public async Task<Data> Run()
    {
        var providerType = Context.App.Providers.ResolveType(Type);
        if (providerType == null)
            return Data.FromError(new Errors.ActionError($"Unknown provider type '{Type}'", "UnknownType", 400));

        return Context.App.Providers.Remove(providerType, Name);
    }
}
