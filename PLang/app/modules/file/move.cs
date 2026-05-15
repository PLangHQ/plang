using app.Variables;
using app.modules.file.code;

namespace app.modules.file;

[System.ComponentModel.Description("Move or rename a file from Source to Destination, optionally overwriting the target")]
[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial data.@this<FileSystem.path> Source { get; init; }
    public partial data.@this<FileSystem.path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<bool> Overwrite { get; init; }

    [Code]
    public partial IFile Files { get; }

    public Task<data.@this> Run() => Task.FromResult(Files.Move(this));
}
