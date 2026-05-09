using App.Variables;
using App.modules.file.code;

namespace App.modules.file;

[ModuleDescription("Read, write, copy, move, delete, and list files through the configured filesystem abstraction")]
[System.ComponentModel.Description("Copy a file or folder from Source to Destination, optionally overwriting and including subfolders")]
[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial Data.@this<FileSystem.Path> Source { get; init; }
    public partial Data.@this<FileSystem.Path> Destination { get; init; }

    [Default(false)]
    public partial Data.@this<bool> Overwrite { get; init; }

    [Default(true)]
    public partial Data.@this<bool> IncludeSubfolders { get; init; }

    [Code]
    public partial IFile Files { get; }

    public Task<Data.@this> Run() => Task.FromResult(Files.Copy(this));
}
