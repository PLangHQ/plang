using app;
using app.variable;

namespace app.module.module;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<global::app.type.path.@this> Path { get; init; }
    public partial data.@this<global::app.type.text.@this>? Namespace { get; init; }

    public async Task<data.@this> Run()
    {
        var app = Context.App;
        var dllPath = (await Path.Value())!;

        // ExistsAsync runs first so the "Module not found" message stays the
        // canonical error for missing DLLs (matches the pre-Stage-5 shape).
        var exists = await dllPath.ExistsAsync();
        if (!exists.Success || (await exists.Value())?.Value != true)
            return Error(new app.error.ServiceError($"Module not found: {dllPath}"));

        // LoadAssemblyAsync gates on Execute — distinct from Read so a Read
        // grant on the folder doesn't accidentally permit code loading.
        var loadResult = await dllPath.LoadAssemblyAsync();
        if (!loadResult.Success) return Error(loadResult.Error!);

        var ns = Namespace == null ? null : (await Namespace.Value())?.ToString();
        var count = app.Module.Discover(global::app.type.item.@this.Lower<System.Reflection.Assembly>(await loadResult.Value())!, ns);
        return Data(new type.module { name = dllPath.FileNameWithoutExtension, actions = count });
    }
}
