using App.Variables;
using App.modules.file.providers;

namespace App.modules.file;

[Example("move file.txt to archive/file.txt", "Source=file.txt, Destination=archive/file.txt")]
[Example("move %source% to %dest%, overwrite", "Source=%source%, Destination=%dest%, Overwrite=true")]
[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial FileSystem.Path Source { get; init; }
    public partial FileSystem.Path Destination { get; init; }

    [Default(false)]
    public partial bool Overwrite { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data.@this> Run() => Task.FromResult(Files.Move(this));
}
