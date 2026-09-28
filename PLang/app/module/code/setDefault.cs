namespace app.module.code;

/// <summary>
/// Sets a named provider as the default for its kind.
/// PLang: set default signing provider to 'custom'
/// </summary>
[Action("setDefault", Cacheable = false)]
public partial class setDefault : IContext
{
    /// <summary>Name of the provider to set as default.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.text.@this> Name { get; init; }

    /// <summary>The kind of provider it is.</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.choice.@this<kind.@this>> Type { get; init; }

    public Task<data.@this> Start() => Type.Use(kind => Name.Use(name =>
        Task.FromResult(Context.App.Code.SetDefault(kind.Value.Interface, name.ToString()))));
}
