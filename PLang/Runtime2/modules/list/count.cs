using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.list;

[Action("count")]
public partial class Count : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.Get(ListName)?.Value;
        if (existing is System.Collections.IList list)
            return Task.FromResult(Data.Ok(list.Count));
        if (existing is System.Collections.IDictionary dict)
            return Task.FromResult(Data.Ok(dict.Count));

        return Task.FromResult(Data.Ok(0));
    }
}
