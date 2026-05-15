using app.Variables;
using app.modules.file.code;

namespace app.modules.file;

[System.ComponentModel.Description("Write Value to a file at Path, creating directories as needed")]
[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial data.@this<FileSystem.path> Path { get; init; }
    public partial data.@this? Value { get; init; }

    [Code]
    public partial IFile Files { get; }

    public Task<data.@this> Run() => Files.Save(this);
}
