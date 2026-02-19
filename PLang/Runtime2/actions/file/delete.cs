using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial PLangPath Path { get; init; }

    [Default(false)]
    public partial bool IgnoreIfNotFound { get; init; }

    public Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;

        if (!Path.IsFile)
        {
            if (IgnoreIfNotFound)
                return Task.FromResult(Data.Ok(new types.@file(Path.Absolute, fs)));
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ServiceError($"File not found: {Path.Raw}", "FileNotFound", 404)));
        }

        fs.File.Delete(Path.Absolute);
        return Task.FromResult(Data.Ok(new types.@file(Path.Absolute, fs)));
    }
}
