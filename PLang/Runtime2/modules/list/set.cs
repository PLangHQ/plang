using PLang.Runtime2.Memory;

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
        var existing = Context.MemoryStack.GetValue(ListName);
        if (existing is not List<object?> list)
            return Task.FromResult(Data.FromError(
                new Errors.ValidationError($"Variable '{ListName}' is not a list")));

        if (Index < 0 || Index >= list.Count)
            return Task.FromResult(Data.FromError(
                new Errors.ValidationError($"Index {Index} out of range (0..{list.Count - 1})")));

        list[Index] = Value;
        Context.MemoryStack.Set(ListName, list);
        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, Memory.Type.FromName("list")));
    }
}
