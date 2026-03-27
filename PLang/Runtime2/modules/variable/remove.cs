using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.variable;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data> Run()
    {
        Context.MemoryStack.Remove(Name);
        return Task.FromResult(Data.Ok());
    }
}
