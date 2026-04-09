using App.Variables;

namespace App.modules.provider;

/// <summary>
/// Sets a named provider as the default for its type.
/// PLang: set default signing provider to 'custom'
/// </summary>
[Action("setDefault", Cacheable = false)]
public partial class setDefault : IContext
{
    /// <summary>Name of the provider to set as default.</summary>
    public partial string Name { get; init; }

    /// <summary>Provider type name (e.g., "signing", "crypto", "identity", "key").</summary>
    public partial string? Type { get; init; }

    public async Task<Data.@this> Run()
    {
        var providerType = Context.App.Providers.ResolveType(Type);
        if (providerType == null)
            return Error(new Errors.ActionError($"Unknown provider type '{Type}'", "UnknownType", 400));

        return Context.App.Providers.SetDefault(providerType, Name);
    }
}
