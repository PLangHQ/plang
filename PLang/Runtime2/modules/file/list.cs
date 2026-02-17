using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.file;

[Action("list")]
public partial class List : IContext
{
    public partial string Path { get; init; }

    [Default("*")]
    public partial string Pattern { get; init; }

    [Default(false)]
    public partial bool Recursive { get; init; }

    public Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;
        var absPath = fs.Path.GetFullPath(Path);

        if (!fs.Directory.Exists(absPath))
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ServiceError($"Directory not found: {Path}", "FileNotFound", 404)));

        var searchOption = Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = fs.Directory.GetFiles(absPath, Pattern, searchOption)
            .Select(f => new types.@file(f, fs))
            .ToArray();

        return Task.FromResult(Data.Ok(files));
    }
}
