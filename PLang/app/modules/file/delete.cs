using app.variables;
using app.modules.file.code;
using app.types;
using Verb = global::app.types.path.permission.verb.@this;
using DeleteVerb = global::app.types.path.permission.verb.Delete;

namespace app.modules.file;

[System.ComponentModel.Description("Delete a file or directory at Path, optionally recursively or ignoring missing targets")]
[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial data.@this<types.path.@this> Path { get; init; }

    [Default(false)]
    public partial data.@this<bool> IgnoreIfNotFound { get; init; }

    [Default(false)]
    public partial data.@this<bool> Recursive { get; init; }

    [Code]
    public partial IFile Files { get; }

    public async Task<data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Delete = new DeleteVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        return Files.Delete(this);
    }
}
