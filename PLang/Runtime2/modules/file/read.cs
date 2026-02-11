using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.file;

[Action("read")]
public partial class Read : IContext
{
    public partial string Path { get; init; }

    public Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;
        var absPath = fs.Path.GetFullPath(Path);

        if (!fs.File.Exists(absPath))
            return Task.FromResult(Data.Fail(
                new Errors.ServiceError($"File not found: {Path}")));

        return Task.FromResult(Data.Ok(new types.@file(absPath, fs)));
    }
}
