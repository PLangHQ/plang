using App;
using App.Variables;

namespace App.modules.module;

[Action("add", Cacheable = false)]
public partial class Add : IContext
{
    public partial string Path { get; init; }
    public partial string? Namespace { get; init; }

    public Task<Data> Run()
    {
        var engine = Context.App!;
        var fs = engine.FileSystem;
        var absPath = fs.Path.GetFullPath(Path);

        if (!fs.File.Exists(absPath))
            return Task.FromResult(Data.FromError(
                new App.Errors.ServiceError($"Module not found: {Path}")));

        var assembly = System.Reflection.Assembly.LoadFrom(absPath);
        var count = engine.Modules.Discover(assembly, Namespace);

        return Task.FromResult(Data.Ok(
            new types.module { name = fs.Path.GetFileNameWithoutExtension(absPath), actions = count }));
    }
}
