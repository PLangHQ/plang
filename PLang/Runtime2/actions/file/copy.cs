using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.file;

[Action("copy", Cacheable = false)]
public partial class Copy : IContext
{
    public partial PLangPath Source { get; init; }
    public partial PLangPath Destination { get; init; }

    [Default(false)]
    public partial bool Overwrite { get; init; }

    [Default(true)]
    public partial bool IncludeSubfolders { get; init; }

    public Task<Data> Run() => Task.FromResult(Source.Copy(this));
}
