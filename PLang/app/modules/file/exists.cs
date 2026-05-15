using app.FileSystem;
using app.Variables;
using app.modules.file.code;

namespace app.modules.file;

[System.ComponentModel.Description("Check whether a file or directory exists at Path and return file info")]
[Example("check if file.txt exists, write to %fileInfo%",
    "file.exists Path([path] file.txt) | variable.set Name([string] %fileInfo%), Value([object] %__data__%)")]
[Action("exists")]
public partial class Exists : IContext
{
    public partial data.@this<FileSystem.Path> Path { get; init; }

    [Code]
    public partial IFile Files { get; }

    public Task<data.@this> Run() => Task.FromResult(Files.Exists(this));
}
