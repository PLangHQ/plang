using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.modules.list;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial int Index { get; init; }
    public partial object? Value { get; init; }

    public Task<Data> Run()
    {
        var data = Context.MemoryStack.Get(ListName);
        if (data?.Value is not List<object?> list)
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        if (Index < 0 || Index >= list.Count)
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Index {Index} out of range (0..{list.Count - 1})")));

        list[Index] = Value;
        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, PLang.Runtime2.Engine.Memory.Type.FromName("list")));
    }
}
