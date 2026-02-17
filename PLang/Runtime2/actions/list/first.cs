using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.list;

[Action("first")]
public partial class First : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.GetValue(ListName);
        if (existing is System.Collections.IList list && list.Count > 0)
            return Task.FromResult(Data.Ok(list[0]));

        return Task.FromResult(Data.Ok());
    }
}
