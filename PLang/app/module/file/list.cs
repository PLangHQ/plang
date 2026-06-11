using app.variable;
using app.type;

namespace app.module.file;

[Action("list")]
public partial class List : IContext
{
    public partial data.@this<path> Path { get; init; }

    [Default("*")]
    public partial data.@this<global::app.type.text.@this> Pattern { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> Recursive { get; init; }

    public async Task<data.@this<global::app.type.list.@this<path>>> Run()
    {
        if (!Path.Success) return data.@this<global::app.type.list.@this<path>>.From(Path);   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        return await (await Path.Value())!.List((await Pattern.Value())!.Clr<string>()!, (await Recursive.Value())!.Value);
    }
}
