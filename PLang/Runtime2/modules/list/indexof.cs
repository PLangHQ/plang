using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.list;

[Action("indexof")]
public partial class IndexOf : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial object? Value { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.GetValue(ListName);
        if (existing is System.Collections.IList list)
            return Task.FromResult(Data.Ok(list.IndexOf(Value)));

        return Task.FromResult(Data.Ok(-1));
    }
}
