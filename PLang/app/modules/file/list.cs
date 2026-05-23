using app.variables;
using app.types;

namespace app.modules.file;

[Action("list")]
public partial class List : IContext
{
    public partial data.@this<path> Path { get; init; }

    [Default("*")]
    public partial data.@this<string> Pattern { get; init; }

    [Default(false)]
    public partial data.@this<bool> Recursive { get; init; }

    public async Task<data.@this<List<path>>> Run()
    {
        if (!Path.Success) return data.@this<List<path>>.From(Path);   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        return await Path.Value!.List(Pattern.Value!, Recursive.Value);
    }
}
