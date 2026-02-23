using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.file;

[Action("save", Cacheable = false)]
public partial class Save : IContext
{
    public partial PLangPath Path { get; init; }
    public partial object Value { get; init; }

    public Task<Data> Run() => Path.Save(this);
}
