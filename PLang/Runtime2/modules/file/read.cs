using PLang.Runtime2.Engine.Memory;

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
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ServiceError($"File not found: {Path}")));

        var file = new types.@file(absPath, fs);
        _ = file.Value; // Eager-read so step cache captures actual content
        return Task.FromResult(Data.Ok(file));
    }
}
