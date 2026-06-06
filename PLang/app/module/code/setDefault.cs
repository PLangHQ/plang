using app.variable;

namespace app.module.code;

/// <summary>
/// Sets a named provider as the default for its type.
/// PLang: set default signing provider to 'custom'
/// </summary>
[Action("setDefault", Cacheable = false)]
public partial class setDefault : IContext
{
    /// <summary>Name of the provider to set as default.</summary>
    public partial data.@this<global::app.type.text.@this> Name { get; init; }

    /// <summary>Provider type name (e.g., "signing", "crypto", "identity", "key").</summary>
    public partial data.@this<global::app.type.text.@this>? Type { get; init; }

    public async Task<data.@this> Run()
    {
        var providerType = Context.App.Code.ResolveType(Type?.Value);
        if (providerType == null)
            return Error(new global::app.error.ActionError($"Unknown provider type '{Type?.Value}'", "UnknownType", 400));

        return Context.App.Code.SetDefault(providerType, Name.Value!);
    }
}
