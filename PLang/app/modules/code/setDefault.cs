using app.Variables;

namespace app.modules.code;

/// <summary>
/// Sets a named provider as the default for its type.
/// PLang: set default signing provider to 'custom'
/// </summary>
[System.ComponentModel.Description("Set a named provider as the default for its type (signing, crypto, identity, key)")]
[Action("setDefault", Cacheable = false)]
public partial class setDefault : IContext
{
    /// <summary>Name of the provider to set as default.</summary>
    public partial data.@this<string> Name { get; init; }

    /// <summary>Provider type name (e.g., "signing", "crypto", "identity", "key").</summary>
    public partial data.@this<string>? Type { get; init; }

    public async Task<data.@this> Run()
    {
        var providerType = Context.App.Code.ResolveType(Type?.Value);
        if (providerType == null)
            return Error(new Errors.ActionError($"Unknown provider type '{Type?.Value}'", "UnknownType", 400));

        return Context.App.Code.SetDefault(providerType, Name.Value!);
    }
}
