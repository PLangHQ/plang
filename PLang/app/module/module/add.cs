namespace app.module.module;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<global::app.type.item.path.@this> Path { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Namespace { get; init; }

    // The DLL comes in through the code registry's one door; the actions it brings join the modules.
    public Task<data.@this> Start() => Path.Use(path => Context.App.Code.Load(path, Context, async (assembly, _) =>
    {
        var ns = Namespace == null ? null : (await Namespace.Value())?.ToString();
        var count = Context.App.module.list.Discover(assembly, ns);
        return Data(new type.module { name = path.FileNameWithoutExtension, actions = count });
    }));
}
