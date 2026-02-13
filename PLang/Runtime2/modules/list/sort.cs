using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.list;

[Action("sort", Cacheable = false)]
public partial class Sort : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    [Default(false)]
    public partial bool Descending { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.GetValue(ListName);
        if (existing is not List<object?> list)
            return Task.FromResult(Data.FromError(
                new Errors.ValidationError($"Variable '{ListName}' is not a list")));

        if (Descending)
            list.Sort((a, b) => Comparer<object>.Default.Compare(b, a));
        else
            list.Sort((a, b) => Comparer<object>.Default.Compare(a, b));

        Context.MemoryStack.Set(ListName, list);
        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, Memory.Type.FromName("list")));
    }
}
