using app;

namespace app.module.action.module;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<global::app.type.item.path.@this> Path { get; init; }
    public partial data.@this<global::app.type.item.text.@this>? Namespace { get; init; }

    public async Task<data.@this> Start()
    {
        var app = Context.App;
        var dllPath = (await Path.Value())!;

        // ExistsAsync runs first so the "Module not found" message stays the
        // canonical error for missing DLLs (matches the pre-Stage-5 shape).
        var exists = await dllPath.ExistsAsync(Context);
        if (!exists.Success || (await exists.Value())?.Value != true)
            return Error(new app.error.ServiceError($"Module not found: {dllPath}"));

        // LoadAssemblyAsync gates on Execute — distinct from Read so a Read
        // grant on the folder doesn't accidentally permit code loading.
        var loadResult = await dllPath.LoadAssemblyAsync(Context);
        if (!loadResult.Success) return Error(loadResult.Error!);

        var ns = Namespace == null ? null : (await Namespace.Value())?.ToString();
        var assembly = (await loadResult.Value()).Clr<System.Reflection.Assembly>()!;
        var count = app.module.list.Discover(assembly, ns);
        // The assembly's plang types and kinds (the closed sets its choice<T> params draw on) come in
        // through the types' one way in.
        var types = app.type.list.Add(assembly, Context);
        if (!types.Success) return types;
        return Data(new type.module { name = dllPath.FileNameWithoutExtension, actions = count });
    }
}
