using app.variables;
using app.types;

namespace app.modules.file;

[System.ComponentModel.Description("Write Value to a file at Path, creating directories as needed")]
[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial data.@this<path> Path { get; init; }
    public partial data.@this? Value { get; init; }

    public async Task<data.@this<path>> Run()
    {
        if (!Path.Success) return global::app.data.@this<path>.From(Path);   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        return global::app.data.@this<path>.From(await Path.Value!.Save(Value));
    }
}
