namespace app.module.code;

/// <summary>
/// Loads a DLL: the types it declares join the app's types, the providers it brings are registered (each
/// names itself).
/// PLang: load code 'my-crypto.dll'
/// </summary>
[Action("load", Cacheable = false)]
public partial class load : IContext
{
    /// <summary>Path to the DLL to load (relative to the goal's folder or the app root, or absolute).</summary>
    [IsNotNull]
    public partial data.@this<global::app.type.item.path.@this> Path { get; init; }

    // The DLL comes in through the code registry's one door; the providers it brings are registered.
    public Task<data.@this> Start() => Path.Use(path => Context.App.Code.Load(path, Context,
        (assembly, types) => Context.App.Code.Register(assembly, types, path, Context)));
}
