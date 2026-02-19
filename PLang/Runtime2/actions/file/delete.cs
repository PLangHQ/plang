using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("delete", Cacheable = false)]
public partial class Delete : IContext
{
    public partial PLangPath Path { get; init; }

    [Default(false)]
    public partial bool IgnoreIfNotFound { get; init; }

    [Default(false)]
    public partial bool Recursive { get; init; }

    public Task<Data> Run() => Task.FromResult(Path.Delete(Recursive, IgnoreIfNotFound));
}
