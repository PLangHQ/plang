using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial PLangPath Source { get; init; }
    public partial PLangPath Destination { get; init; }
    public partial bool Overwrite { get; init; }

    public Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;

        if (!Source.IsFile)
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ServiceError($"File not found: {Source.Raw}", "FileNotFound", 404)));

        var destDir = Destination.Directory;
        if (!string.IsNullOrEmpty(destDir) && !fs.Directory.Exists(destDir))
            fs.Directory.CreateDirectory(destDir);

        fs.File.Move(Source.Absolute, Destination.Absolute, Overwrite);
        return Task.FromResult(Data.Ok(new types.@file(Destination.Absolute, fs, source: Source.Absolute)));
    }
}
