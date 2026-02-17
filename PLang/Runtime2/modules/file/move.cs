using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.file;

[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial string Source { get; init; }
    public partial string Destination { get; init; }
    public partial bool Overwrite { get; init; }

    public Task<Data> Run()
    {
        var fs = Context.Engine!.FileSystem;
        var absSource = fs.Path.GetFullPath(Source);
        var absDest = fs.Path.GetFullPath(Destination);

        if (!fs.File.Exists(absSource))
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ServiceError($"File not found: {Source}", "FileNotFound", 404)));

        var destDir = fs.Path.GetDirectoryName(absDest);
        if (!string.IsNullOrEmpty(destDir) && !fs.Directory.Exists(destDir))
            fs.Directory.CreateDirectory(destDir);

        fs.File.Move(absSource, absDest, Overwrite);
        return Task.FromResult(Data.Ok(new types.@file(absDest, fs, source: absSource)));
    }
}
