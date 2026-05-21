using app.filesystem;
using app.variables;
using app.modules.file.code;
using app.types;
using Verb = global::app.filesystem.permission.verb.@this;
using ReadVerb = global::app.filesystem.permission.verb.Read;

namespace app.modules.file;

[System.ComponentModel.Description("Check whether a file or directory exists at Path and return file info")]
[Example("check if file.txt exists, write to %fileInfo%",
    "file.exists Path([path] file.txt) | variable.set Name([string] %fileInfo%), Value([object] %!data%)")]
[Action("exists")]
public partial class Exists : IContext
{
    public partial data.@this<filesystem.path> Path { get; init; }

    [Code]
    public partial IFile Files { get; }

    public async Task<data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        return Files.Exists(this);
    }
}
