using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.variable;

[Action("clear")]
public partial class Clear : IContext
{
    public Task<Data> Run()
    {
        Context.MemoryStack.Clear();
        return Task.FromResult(Data.Ok(
            new types.variable { }));
    }
}
