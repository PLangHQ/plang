using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.library;

[Action("load", Cacheable = false)]
public partial class Load : IContext
{
    public partial string Path { get; init; }
    public partial string? Namespace { get; init; }

    public Task<Data> Run()
    {
        var engine = Context.Engine!;
        var fs = engine.FileSystem;
        var absPath = fs.Path.GetFullPath(Path);

        if (!fs.File.Exists(absPath))
            return Task.FromResult(Data.FromError(
                new Errors.ServiceError($"Library not found: {Path}")));

        var assembly = System.Reflection.Assembly.LoadFrom(absPath);
        var lib = new Library(fs.Path.GetFileNameWithoutExtension(absPath), assembly);
        lib.Discover(Namespace);
        engine.Libraries.Add(lib);

        return Task.FromResult(Data.Ok(
            new types.library { name = lib.Name, actions = lib.Count }));
    }
}
