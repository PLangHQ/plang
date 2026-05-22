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
        if (Path.Value is filepath fp)
            return await fp.Delete(Recursive.Value, IgnoreIfNotFound.Value);
        return await Path.Value!.Delete();
    }
}
