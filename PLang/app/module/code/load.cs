namespace app.module.code;

/// <summary>
/// Loads a provider from a DLL or registers a provider instance.
/// PLang: load provider 'my-crypto.dll' as 'custom-crypto'
/// </summary>
[Action("load", Cacheable = false)]
public partial class load : IContext
{
    /// <summary>Path to the DLL to load (relative to app root or absolute).</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.path.@this> Path { get; init; }

    /// <summary>Optional display name for the provider (not currently used — provider supplies its own Name).</summary>
    public partial data.@this<global::app.type.item.text.@this>? Name { get; init; }

    // The DLL comes in through the code registry's one door; the providers it brings are registered.
    public Task<data.@this> Start() => Path.Use(path => Context.App.Code.Load(path, Context,
        (assembly, types) => Context.App.Code.Register(assembly, types, path, Context)));
}
