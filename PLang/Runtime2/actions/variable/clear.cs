using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.variable;

[Action("clear", Cacheable = false)]
public partial class Clear : IContext
{
    public Task<Data> Run()
    {
        Context.MemoryStack.Clear();
        return Task.FromResult(Data.Ok(
            new types.variable { }));
    }
}
