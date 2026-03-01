using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.file;

[Action("move", Cacheable = false)]
public partial class Move : IContext
{
    public partial PLangPath Source { get; init; }
    public partial PLangPath Destination { get; init; }

    [Default(false)]
    public partial bool Overwrite { get; init; }

    public Task<Data> Run() => Task.FromResult(Source.Move(this));
}
