using App.Variables;

namespace App.modules.provider;

/// <summary>
/// Lists registered providers, optionally filtered by type.
/// PLang: list signing providers
/// </summary>
[ModuleDescription("Manage the provider registry: list, load, remove, and change defaults for signing, crypto, etc.")]
[System.ComponentModel.Description("List registered providers, optionally filtered by type (signing, crypto, identity, key)")]
[Action("list", Cacheable = false)]
public partial class list : IContext
{
    /// <summary>Optional provider type filter (e.g., "signing", "crypto", "identity", "key"). Omit to list all.</summary>
    public partial Data.@this<string>? Type { get; init; }

    public async Task<Data.@this> Run()
    {
        if (string.IsNullOrEmpty(Type?.Value))
            return Data(Context.App.Providers.List());

        var providerType = Context.App.Providers.ResolveType(Type.Value!);
        if (providerType == null)
            return Error(new Errors.ActionError($"Unknown provider type '{Type.Value}'", "UnknownType", 400));

        return Context.App.Providers.List(providerType);
    }
}
