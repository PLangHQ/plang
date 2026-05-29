using app.variable;
using app.types;

namespace app.modules.file;

[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial data.@this<path> Path { get; init; }
    public partial data.@this? Value { get; init; }

    public async Task<data.@this<path>> Run()
    {
        if (!Path.Success) return data.@this<path>.From(Path);   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        return await Path.Value!.Save(Value);
    }
}
