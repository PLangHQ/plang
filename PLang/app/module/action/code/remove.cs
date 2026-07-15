using app.variable;

namespace app.module.action.code;

/// <summary>
/// Removes a named provider from the registry.
/// PLang: remove provider 'custom-crypto' from signing
/// </summary>
[Action("remove", Cacheable = false)]
public partial class remove : IContext
{
    /// <summary>Name of the provider to remove.</summary>
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    /// <summary>Provider type name (e.g., "signing", "crypto", "identity", "key").</summary>
    public partial data.@this<global::app.type.item.text.@this>? Type { get; init; }

    public async Task<data.@this> Run()
    {
        var typeName = Type == null ? null : (await Type.Value())?.Clr<string>();
        var providerType = Context.App.Code.ResolveType(typeName);
        if (providerType == null)
            return Error(new global::app.error.ActionError($"Unknown provider type '{typeName}'", "UnknownType", 400));

        return Context.App.Code.Remove(providerType, (await Name.Value())!.Clr<string>()!);
    }
}
