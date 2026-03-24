using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.list;

[Action("last")]
public partial class Last : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.Get(ListName)?.Value;
        if (existing is System.Collections.IList list && list.Count > 0)
            return Task.FromResult(Data.Ok(list[list.Count - 1]));

        return Task.FromResult(Data.Ok());
    }
}
