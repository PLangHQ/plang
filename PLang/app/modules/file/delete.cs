using app.variables;
using app.types;

namespace app.modules.file;

[System.ComponentModel.Description("Delete a file or directory at Path, optionally recursively or ignoring missing targets")]
[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial data.@this<path> Path { get; init; }

    [Default(false)]
    public partial data.@this<bool> IgnoreIfNotFound { get; init; }

    [Default(false)]
    public partial data.@this<bool> Recursive { get; init; }

    public async Task<data.@this> Run()
    {
        if (!Path.Success) return Path;   // codeanalyzer v1 F4 — typed scheme error, not an NRE
        return await Path.Value!.Delete(Recursive.Value, IgnoreIfNotFound.Value);
    }
}
