using App.Types;
using App.Variables;
using App.modules.file.code;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using ReadVerb = global::App.FileSystem.Permission.Verb.Read;

namespace App.modules.file;

[System.ComponentModel.Description("List files in a directory matching an optional glob pattern, optionally recursing into subdirectories")]
[Example("list files in docs/ recursive, write to %files%",
    "file.list Path([path] docs/), Recursive([bool] true) | variable.set Name([string] %files%), Value([object] %__data__%)")]
[Action("list")]
public partial class List : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }

    [Default("*")]
    public partial Data.@this<string> Pattern { get; init; }

    [Default(false)]
    public partial Data.@this<bool> Recursive { get; init; }

    [Code]
    public partial IFile Files { get; }

    public async Task<Data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        return Files.List(this);
    }
}
