using app.variables;
using app.types;

namespace app.modules.file;

[System.ComponentModel.Description("Check whether a file or directory exists at Path and return file info")]
[Example("check if file.txt exists, write to %fileInfo%",
    "file.exists Path([path] file.txt) | variable.set Name([string] %fileInfo%), Value([object] %!data%)")]
[Action("exists")]
public partial class Exists : IContext
{
    public partial data.@this<global::app.types.path.@this> Path { get; init; }

    public async Task<data.@this> Run()
    {
        if (Path.Value is global::app.types.path.file.@this fp)
            return await fp.ExistsPathAsync();
        return await Path.Value!.ExistsAsync();
    }
}
