using app.variable;

namespace app.module.code;

/// <summary>
/// Lists registered providers, optionally filtered by type.
/// PLang: list signing providers
/// </summary>
[Action("list", Cacheable = false)]
public partial class list : IContext
{
    /// <summary>Optional provider type filter (e.g., "signing", "crypto", "identity", "key"). Omit to list all.</summary>
    public partial data.@this<global::app.type.text.@this>? Type { get; init; }

    public async Task<data.@this> Run()
    {
        if (string.IsNullOrEmpty(Type?.Value))
            return Data(Context.App.Code.List());

        var providerType = Context.App.Code.ResolveType(Type.Value!);
        if (providerType == null)
            return Error(new global::app.error.ActionError($"Unknown provider type '{Type.Value}'", "UnknownType", 400));

        return Context.App.Code.List(providerType);
    }
}
