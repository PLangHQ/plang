using App;
using App.Variables;

namespace App.modules.module;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial Data.@this<string> Path { get; init; }
    public partial Data.@this<string>? Namespace { get; init; }

    public Task<Data.@this> Run()
    {
        var app = Context.App!;
        var fs = app.FileSystem;
        var absPath = fs.Path.GetFullPath(Path.Value!);

        if (!fs.File.Exists(absPath))
            return Task.FromResult(Error(
                new App.Errors.ServiceError($"Module not found: {Path.Value}")));

        var assembly = System.Reflection.Assembly.LoadFrom(absPath);
        var count = app.Modules.Discover(assembly, Namespace?.Value);

        return Task.FromResult(Data(
            new types.module { name = fs.Path.GetFileNameWithoutExtension(absPath), actions = count }));
    }
}
