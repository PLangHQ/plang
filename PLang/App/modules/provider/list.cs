using App.Variables;

namespace App.modules.provider;

/// <summary>
/// Lists registered providers, optionally filtered by type.
/// PLang: list signing providers
/// </summary>
[Action("list", Cacheable = false)]
public partial class list : IContext
{
    /// <summary>Optional provider type filter (e.g., "signing", "crypto", "identity", "key"). Omit to list all.</summary>
    public partial string? Type { get; init; }

    public async Task<Data> Run()
    {
        if (string.IsNullOrEmpty(Type))
            return Data.Ok(Context.App.Providers.List());

        var providerType = Context.App.Providers.ResolveType(Type);
        if (providerType == null)
            return Data.FromError(new Errors.ActionError($"Unknown provider type '{Type}'", "UnknownType", 400));

        return Context.App.Providers.List(providerType);
    }
}
