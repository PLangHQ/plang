using app.variables;
using app.modules.file.code;
using app.types;
using Verb = global::app.types.path.permission.verb.@this;
using ReadVerb = global::app.types.path.permission.verb.Read;

namespace app.modules.file;

[System.ComponentModel.Description("List files in a directory matching an optional glob pattern, optionally recursing into subdirectories")]
[Example("list files in docs/ recursive, write to %files%",
    "file.list Path([path] docs/), Recursive([bool] true) | variable.set Name([string] %files%), Value([object] %!data%)")]
[Action("list")]
public partial class List : IContext
{
    public partial data.@this<types.path.@this> Path { get; init; }

    [Default("*")]
    public partial data.@this<string> Pattern { get; init; }

    [Default(false)]
    public partial data.@this<bool> Recursive { get; init; }

    [Code]
    public partial IFile Files { get; }

    public async Task<data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        return Files.List(this);
    }
}
