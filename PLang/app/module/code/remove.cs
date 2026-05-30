using app.variable;

namespace app.module.code;

/// <summary>
/// Removes a named provider from the registry.
/// PLang: remove provider 'custom-crypto' from signing
/// </summary>
[Action("remove", Cacheable = false)]
public partial class remove : IContext
{
    /// <summary>Name of the provider to remove.</summary>
    public partial data.@this<string> Name { get; init; }

    /// <summary>Provider type name (e.g., "signing", "crypto", "identity", "key").</summary>
    public partial data.@this<string>? Type { get; init; }

    public async Task<data.@this> Run()
    {
        var providerType = Context.App.Code.ResolveType(Type?.Value);
        if (providerType == null)
            return Error(new global::app.error.ActionError($"Unknown provider type '{Type?.Value}'", "UnknownType", 400));

        return Context.App.Code.Remove(providerType, Name.Value!);
    }
}
