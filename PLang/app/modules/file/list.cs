using app.variables;
using app.types;

namespace app.modules.file;

[System.ComponentModel.Description("List files in a directory matching an optional glob pattern, optionally recursing into subdirectories")]
[Example("list files in docs/ recursive, write to %files%",
    "file.list Path([path] docs/), Recursive([bool] true) | variable.set Name([string] %files%), Value([object] %!data%)")]
[Action("list")]
public partial class List : IContext
{
    public partial data.@this<path> Path { get; init; }

    [Default("*")]
    public partial data.@this<string> Pattern { get; init; }

    [Default(false)]
    public partial data.@this<bool> Recursive { get; init; }

    public async Task<data.@this> Run()
    {
        if (Path.Value is filepath fp)
            return await fp.List(Pattern.Value!, Recursive.Value);
        return await Path.Value!.List();
    }
}
