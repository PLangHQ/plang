using App.FileSystem;
using App.Variables;
using App.modules.file.providers;

namespace App.modules.file;

[System.ComponentModel.Description("Check whether a file or directory exists at Path and return file info")]
[Example("check if file.txt exists, write to %fileInfo%",
    "file.exists Path([path] file.txt) | variable.set Name([string] %fileInfo%), Value([object] %__data__%)")]
[Action("exists")]
public partial class Exists : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data.@this> Run() => Task.FromResult(Files.Exists(this));
}
