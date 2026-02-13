using PLang.Runtime2.Memory;

namespace PLang.Runtime2.modules.list;

[Action("unique")]
public partial class Unique : IContext
{
    [VariableName]
    public partial string ListName { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.GetValue(ListName);
        if (existing is not List<object?> list)
            return Task.FromResult(Data.FromError(
                new Errors.ValidationError($"Variable '{ListName}' is not a list")));

        var distinct = list.Distinct().ToList();
        return Task.FromResult(Data.Ok(new types.list { count = distinct.Count, value = distinct }, Memory.Type.FromName("list")));
    }
}
