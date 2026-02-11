using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.file;

[Action("exists")]
public partial class Exists : IContext
{
    public partial string Path { get; init; }

    public Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;
        var absPath = fs.Path.GetFullPath(Path);
        return Task.FromResult(Data.Ok(new types.@file(absPath, fs)));
    }
}
