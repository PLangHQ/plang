namespace app.module.code;

/// <summary>
/// Removes a named provider of one kind from the registry.
/// PLang: remove provider 'custom-crypto' from signing
/// </summary>
[Action("remove", Cacheable = false)]
public partial class remove : IContext
{
    /// <summary>Name of the provider to remove.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    /// <summary>The kind of provider it is.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<kind.@this>> Type { get; init; }

    public Task<data.@this> Start() => Type.Use(kind => Name.Use(name =>
        Task.FromResult(Context.App.Code.Remove(kind.Value.Interface, name.ToString()))));
}
