using app.types.path;
using app.variables;
using app.modules.file.code;
using app.types;
using Verb = global::app.types.path.permission.verb.@this;
using ReadVerb = global::app.types.path.permission.verb.Read;

namespace app.modules.file;

[System.ComponentModel.Description("Check whether a file or directory exists at Path and return file info")]
[Example("check if file.txt exists, write to %fileInfo%",
    "file.exists Path([path] file.txt) | variable.set Name([string] %fileInfo%), Value([object] %!data%)")]
[Action("exists")]
public partial class Exists : IContext
{
    public partial data.@this<types.path.@this> Path { get; init; }

    [Code]
    public partial IFile Files { get; }

    public async Task<data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        return Files.Exists(this);
    }
}
