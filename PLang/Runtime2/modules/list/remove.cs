using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.list;

[Action("remove", Cacheable = false)]
public partial class Remove : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    public partial object? Value { get; init; }
    [Default(-1)]
    public partial int AtIndex { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.GetValue(ListName);
        if (existing is not List<object?> list)
            return Task.FromResult(Data.FromError(
                new Errors.ValidationError($"Variable '{ListName}' is not a list")));

        if (AtIndex >= 0)
        {
            if (AtIndex < list.Count)
                list.RemoveAt(AtIndex);
        }
        else
        {
            list.Remove(Value);
        }

        Context.MemoryStack.Set(ListName, list);
        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, Memory.Type.FromName("list")));
    }
}
