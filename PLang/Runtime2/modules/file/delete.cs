using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.file.providers;

namespace PLang.Runtime2.modules.file;

[Example("delete file.txt", "Path=file.txt")]
[Example("delete %path%, ignore if not found", "Path=%path%, IgnoreIfNotFound=true")]
[Example("delete temp/, recursive", "Path=temp/, Recursive=true")]
[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial PLangPath Path { get; init; }

    [Default(false)]
    public partial bool IgnoreIfNotFound { get; init; }

    [Default(false)]
    public partial bool Recursive { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data> Run() => Task.FromResult(Files.Delete(this));
}
