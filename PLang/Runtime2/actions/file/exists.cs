using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("exists")]
public partial class Exists : IContext
{
    public partial PLangPath Path { get; init; }

    public Task<Data> Run() => Task.FromResult(Path.AsFile());
}
