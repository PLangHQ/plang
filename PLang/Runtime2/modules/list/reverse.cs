using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.list;

[Action("reverse", Cacheable = false)]
public partial class Reverse : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.GetValue(ListName);
        if (existing is not List<object?> list)
            return Task.FromResult(Data.FromError(
                new Errors.ValidationError($"Variable '{ListName}' is not a list")));

        list.Reverse();
        Context.MemoryStack.Set(ListName, list);
        return Task.FromResult(Data.Ok(new types.list { count = list.Count, value = list }, Memory.Type.FromName("list")));
    }
}
