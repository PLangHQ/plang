using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("read")]
public partial class Read : IContext
{
    public partial PLangPath Path { get; init; }

    public Task<Data> Run() => Task.FromResult(Path.Read());
}
