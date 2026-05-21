using App.Types;
using App.Variables;
using App.modules.file.code;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using DeleteVerb = global::App.FileSystem.Permission.Verb.Delete;

namespace App.modules.file;

[System.ComponentModel.Description("Delete a file or directory at Path, optionally recursively or ignoring missing targets")]
[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }

    [Default(false)]
    public partial Data.@this<bool> IgnoreIfNotFound { get; init; }

    [Default(false)]
    public partial Data.@this<bool> Recursive { get; init; }

    [Code]
    public partial IFile Files { get; }

    public async Task<Data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Delete = new DeleteVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        return Files.Delete(this);
    }
}
