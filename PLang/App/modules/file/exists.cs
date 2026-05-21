using App.FileSystem;
using App.Types;
using App.Variables;
using App.modules.file.code;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using ReadVerb = global::App.FileSystem.Permission.Verb.Read;

namespace App.modules.file;

[System.ComponentModel.Description("Check whether a file or directory exists at Path and return file info")]
[Example("check if file.txt exists, write to %fileInfo%",
    "file.exists Path([path] file.txt) | variable.set Name([string] %fileInfo%), Value([object] %!data%)")]
[Action("exists")]
public partial class Exists : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }

    [Code]
    public partial IFile Files { get; }

    public async Task<Data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        return Files.Exists(this);
    }
}
