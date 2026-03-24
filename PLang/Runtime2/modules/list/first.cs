using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.list;

[Action("first")]
public partial class First : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.Get(ListName)?.Value;
        if (existing is System.Collections.IList list && list.Count > 0)
            return Task.FromResult(Data.Ok(list[0]));

        return Task.FromResult(Data.Ok());
    }
}
