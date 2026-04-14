using App.FileSystem;
using App.Variables;
using App.modules.file.providers;

namespace App.modules.file;

[Example("check if file.txt exists, write to %fileInfo%", "Path=file.txt")]
[Example("does %path% exist, write to %result%", "Path=%path%")]
[Action("exists")]
public partial class Exists : IContext
{
    public partial Data.@this<FileSystem.Path> Path { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data.@this> Run() => Task.FromResult(Files.Exists(this));
}
