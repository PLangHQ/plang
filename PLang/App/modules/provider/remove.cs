using App.Variables;

namespace App.modules.provider;

/// <summary>
/// Removes a named provider from the registry.
/// PLang: remove provider 'custom-crypto' from signing
/// </summary>
[System.ComponentModel.Description("Remove a named provider from the registry, optionally filtering by provider type")]
[Action("remove", Cacheable = false)]
public partial class remove : IContext
{
    /// <summary>Name of the provider to remove.</summary>
    public partial Data.@this<string> Name { get; init; }

    /// <summary>Provider type name (e.g., "signing", "crypto", "identity", "key").</summary>
    public partial Data.@this<string>? Type { get; init; }

    public async Task<Data.@this> Run()
    {
        var providerType = Context.App.Providers.ResolveType(Type?.Value);
        if (providerType == null)
            return Error(new Errors.ActionError($"Unknown provider type '{Type?.Value}'", "UnknownType", 400));

        return Context.App.Providers.Remove(providerType, Name.Value!);
    }
}
