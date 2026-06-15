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
        var typeName = Type == null ? null : (await Type.Value())?.Clr<string>();

        System.Collections.Generic.IReadOnlyList<ICode> providers;
        if (string.IsNullOrEmpty(typeName))
            providers = Context.App.Code.List();
        else
        {
            var providerType = Context.App.Code.ResolveType(typeName);
            if (providerType == null)
                return Error(new global::app.error.ActionError($"Unknown provider type '{typeName}'", "UnknownType", 400));
            providers = Context.App.Code.List(providerType);
        }

        // Providers are plumbing — PLang sees their names (list<text>), not the CLR instances.
        return Data(providers.Select(p => p.Name).ToList());
    }
}
