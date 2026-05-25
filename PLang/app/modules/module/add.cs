using app;
using app.variables;

namespace app.modules.module;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<global::app.types.path.@this> Path { get; init; }
    public partial data.@this<string>? Namespace { get; init; }

    public async Task<data.@this> Run()
    {
        var app = Context.App!;
        var dllPath = Path.Value!;

        // LoadAssemblyAsync gates on Execute — distinct from Read so a Read
        // grant on the folder doesn't accidentally permit code loading.
        var loadResult = await dllPath.LoadAssemblyAsync();
        if (!loadResult.Success) return Error(loadResult.Error!);

        var count = app.Modules.Discover(loadResult.Value!, Namespace?.Value);
        return Data(new types.module { name = dllPath.FileNameWithoutExtension, actions = count });
    }
}
