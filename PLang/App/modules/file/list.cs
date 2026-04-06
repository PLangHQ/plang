using App.Variables;
using App.modules.file.providers;

namespace App.modules.file;

[Example("list files in docs/, write to %files%", "Path=docs/")]
[Example("list files in %dir% matching *.txt, recursive, write to %files%", "Path=%dir%, Pattern=*.txt, Recursive=true")]
[Action("list")]
public partial class List : IContext
{
    public partial FileSystem.Path Path { get; init; }

    [Default("*")]
    public partial string Pattern { get; init; }

    [Default(false)]
    public partial bool Recursive { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data.@this> Run() => Task.FromResult(Files.List(this));
}
