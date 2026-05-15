using app.Variables;
using app.modules.file.code;

namespace app.modules.file;

[System.ComponentModel.Description("Delete a file or directory at Path, optionally recursively or ignoring missing targets")]
[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial data.@this<FileSystem.path> Path { get; init; }

    [Default(false)]
    public partial data.@this<bool> IgnoreIfNotFound { get; init; }

    [Default(false)]
    public partial data.@this<bool> Recursive { get; init; }

    [Code]
    public partial IFile Files { get; }

    public Task<data.@this> Run() => Task.FromResult(Files.Delete(this));
}
