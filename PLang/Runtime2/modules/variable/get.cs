using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.variable;

[Action("get")]
public partial class Get : IContext
{
    [VariableName]
    public partial string Name { get; init; }

    public Task<Data> Run()
    {
        return Task.FromResult(Context.MemoryStack.Get(Name) ?? Data.Ok(null));
    }
}
