using app.variable;
using app.type;

namespace app.module.file;

[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial data.@this<path> Path { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> IgnoreIfNotFound { get; init; }

    [Default(false)]
    public partial data.@this<global::app.type.@bool.@this> Recursive { get; init; }

    public async Task<data.@this<path>> Run()
    {
        if (!Path.Success) return Path;   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        return await Path.Value!.Delete(Recursive.Value, IgnoreIfNotFound.Value);
    }
}
