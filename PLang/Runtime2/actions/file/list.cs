using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("list")]
public partial class List : IContext
{
    public partial PLangPath Path { get; init; }

    [Default("*")]
    public partial string Pattern { get; init; }

    [Default(false)]
    public partial bool Recursive { get; init; }

    public Task<Data> Run() => Task.FromResult(Path.List(Pattern, Recursive));
}
