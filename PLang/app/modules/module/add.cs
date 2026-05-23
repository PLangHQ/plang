using app;
using app.variables;

namespace app.modules.module;

[ModuleDescription("Load and unload external module DLLs at runtime to extend the available action catalog")]
[System.ComponentModel.Description("Load a module DLL from Path and register its actions into the current app's action catalog")]
[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial data.@this<string> Path { get; init; }
    public partial data.@this<string>? Namespace { get; init; }

    public Task<data.@this> Run()
    {
        var app = Context.App!;
        var absPath = System.IO.Path.GetFullPath(Path.Value!);

        if (!System.IO.File.Exists(absPath))
            return Task.FromResult(Error(
                new app.errors.ServiceError($"Module not found: {Path.Value}")));

        var assembly = System.Reflection.Assembly.LoadFrom(absPath);
        var count = app.Modules.Discover(assembly, Namespace?.Value);

        return Task.FromResult(Data(
            new types.module { name = System.IO.Path.GetFileNameWithoutExtension(absPath), actions = count }));
    }
}
