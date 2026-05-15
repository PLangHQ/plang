using app.Variables;
using app.modules.file.code;

namespace app.modules.file;

[ModuleDescription("Read, write, copy, move, delete, and list files through the configured filesystem abstraction")]
[System.ComponentModel.Description("Copy a file or folder from Source to Destination, optionally overwriting and including subfolders")]
[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial data.@this<FileSystem.Path> Source { get; init; }
    public partial data.@this<FileSystem.Path> Destination { get; init; }

    [Default(false)]
    public partial data.@this<bool> Overwrite { get; init; }

    [Default(true)]
    public partial data.@this<bool> IncludeSubfolders { get; init; }

    [Code]
    public partial IFile Files { get; }

    public Task<data.@this> Run() => Task.FromResult(Files.Copy(this));
}
