using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.list;

[Action("get")]
public partial class Get : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial int Index { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.Get(ListName)?.Value;
        if (existing is not System.Collections.IList list)
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        if (Index < 0 || Index >= list.Count)
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Index {Index} out of range (0..{list.Count - 1})")));

        return Task.FromResult(Data.Ok(list[Index]));
    }
}
