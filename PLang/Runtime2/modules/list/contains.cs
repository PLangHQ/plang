using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.list;

[Action("contains")]
public partial class Contains : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial object? Value { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.Get(ListName)?.Value;
        if (existing is System.Collections.IList list)
            return Task.FromResult(Data.Ok(list.Contains(Value)));
        if (existing is System.Collections.IDictionary dict && Value is string key)
            return Task.FromResult(Data.Ok(dict.Contains(key)));

        return Task.FromResult(Data.Ok(false));
    }
}
