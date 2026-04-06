using App.Variables;
using App.modules.file.providers;

namespace App.modules.file;

[Example("copy file.txt to backup/file.txt", "Source=file.txt, Destination=backup/file.txt")]
[Example("copy %source% to %dest%, overwrite", "Source=%source%, Destination=%dest%, Overwrite=true")]
[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial PLangPath Source { get; init; }
    public partial PLangPath Destination { get; init; }

    [Default(false)]
    public partial bool Overwrite { get; init; }

    [Default(true)]
    public partial bool IncludeSubfolders { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data> Run() => Task.FromResult(Files.Copy(this));
}
