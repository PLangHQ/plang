using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.list;

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
                new PLang.Runtime2.Engine.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        var distinct = list.Distinct().Cast<object?>().ToList();
        return Task.FromResult(Data.Ok(distinct, PLang.Runtime2.Engine.Memory.Type.FromName("list")));
    }
}
