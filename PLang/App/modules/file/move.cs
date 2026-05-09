using App.Variables;
using App.modules.file.code;

namespace App.modules.file;

[System.ComponentModel.Description("Move or rename a file from Source to Destination, optionally overwriting the target")]
[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial Data.@this<FileSystem.Path> Source { get; init; }
    public partial Data.@this<FileSystem.Path> Destination { get; init; }

    [Default(false)]
    public partial Data.@this<bool> Overwrite { get; init; }

    [Code]
    public partial IFile Files { get; }

    public Task<Data.@this> Run() => Task.FromResult(Files.Move(this));
}
