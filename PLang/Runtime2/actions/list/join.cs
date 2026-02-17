using PLang.Runtime2.Engine.Memory;

namespace PLang.Runtime2.actions.list;

[Action("join")]
public partial class Join : IContext
{
    [VariableName]
    public partial string ListName { get; init; }
    [Default(",")]
    public partial string Separator { get; init; }

    public Task<Data> Run()
    {
        var existing = Context.MemoryStack.GetValue(ListName);
        if (existing is not System.Collections.IList list)
            return Task.FromResult(Data.FromError(
                new PLang.Runtime2.Engine.Errors.ValidationError($"Variable '{ListName}' is not a list")));

        var strings = new List<string>();
        foreach (var item in list)
            strings.Add(item?.ToString() ?? "");

        var result = string.Join(Separator, strings);
        return Task.FromResult(Data.Ok(result, PLang.Runtime2.Engine.Memory.Type.String));
    }
}
