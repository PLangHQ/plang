using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("list")]
public partial class List : IContext
{
    public partial PLangPath Path { get; init; }

    [Default("*")]
    public partial string Pattern { get; init; }

    [Default(false)]
    public partial bool Recursive { get; init; }

    public Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;

        if (!Path.IsDirectory)
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ServiceError($"Directory not found: {Path.Raw}", "FileNotFound", 404)));

        var searchOption = Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = fs.Directory.GetFiles(Path.Absolute, Pattern, searchOption)
            .Select(f => new types.@file(f, fs))
            .ToArray();

        return Task.FromResult(Data.Ok(files));
    }
}
