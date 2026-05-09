using App.Variables;
using App.modules.file.code;

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

    public Task<Data.@this> Run() => Task.FromResult(Files.Delete(this));
}
