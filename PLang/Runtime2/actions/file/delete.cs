using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial string Path { get; init; }

    [Default(false)]
    public partial bool IgnoreIfNotFound { get; init; }

    public Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;
        var absPath = fs.Path.GetFullPath(Path);

        if (!fs.File.Exists(absPath))
        {
            if (IgnoreIfNotFound)
                return Task.FromResult(Data.Ok(new types.@file(absPath, fs)));
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ServiceError($"File not found: {Path}", "FileNotFound", 404)));
        }

        fs.File.Delete(absPath);
        return Task.FromResult(Data.Ok(new types.@file(absPath, fs)));
    }
}
