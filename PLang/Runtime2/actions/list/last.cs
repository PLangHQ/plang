using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.list;

[Action("last")]
public partial class Last : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.GetValue(ListName);
        if (existing is System.Collections.IList list && list.Count > 0)
            return Task.FromResult(Data.Ok(list[list.Count - 1]));

        return Task.FromResult(Data.Ok());
    }
}
