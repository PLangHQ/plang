using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.file.providers;

namespace PLang.Runtime2.modules.file;

[Example("read file.txt, write to %content%", "Path=file.txt")]
[Example("read %path%, write to %data%", "Path=%path%")]
[Action("read")]
public partial class Read : IContext
{
    public partial PLangPath Path { get; init; }

    [Provider]
    public partial IFileProvider Files { get; }

    public Task<Data> Run() => Task.FromResult(Files.Read(this));
}
