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

    public async Task<data.@this<object>> Run()
    {
        if (!Path.Success) return global::app.data.@this<object>.From(Path);   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        return global::app.data.@this<object>.From(await Path.Value!.List(Pattern.Value!, Recursive.Value));
    }
}
