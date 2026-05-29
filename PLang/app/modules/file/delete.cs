using app.variable;
using app.type;

namespace app.modules.file;

[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial data.@this<path> Path { get; init; }

    [Default(false)]
    public partial data.@this<bool> IgnoreIfNotFound { get; init; }

    [Default(false)]
    public partial data.@this<bool> Recursive { get; init; }

    public async Task<data.@this<path>> Run()
    {
        if (!Path.Success) return Path;   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        return await Path.Value!.Delete(Recursive.Value, IgnoreIfNotFound.Value);
    }
}
