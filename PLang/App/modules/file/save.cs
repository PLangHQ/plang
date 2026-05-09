using App.Variables;
using App.modules.file.code;

namespace App.modules.file;

[System.ComponentModel.Description("Write Value to a file at Path, creating directories as needed")]
[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }
    public partial Data.@this? Value { get; init; }

    [Provider]
    public partial IFile Files { get; }

    public Task<Data.@this> Run() => Files.Save(this);
}
