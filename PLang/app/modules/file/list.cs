using app.variables;
using app.modules.file.code;

namespace app.modules.file;

[System.ComponentModel.Description("List files in a directory matching an optional glob pattern, optionally recursing into subdirectories")]
[Example("list files in docs/ recursive, write to %files%",
    "file.list Path([path] docs/), Recursive([bool] true) | variable.set Name([string] %files%), Value([object] %__data__%)")]
[Action("list")]
public partial class List : IContext
{
    public partial data.@this<filesystem.path> Path { get; init; }

    [Default("*")]
    public partial data.@this<string> Pattern { get; init; }

    [Default(false)]
    public partial data.@this<bool> Recursive { get; init; }

    [Code]
    public partial IFile Files { get; }

    public Task<data.@this> Run() => Task.FromResult(Files.List(this));
}
