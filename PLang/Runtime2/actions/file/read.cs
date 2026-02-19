using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("read")]
public partial class Read : IContext
{
    public partial PLangPath Path { get; init; }

    public Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;

        if (!Path.IsFile)
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ServiceError($"File not found: {Path.Raw}")));

        var file = new types.@file(Path.Absolute, fs);
        _ = file.Value; // Eager-read so step cache captures actual content
        return Task.FromResult(Data.Ok(file));
    }
}
